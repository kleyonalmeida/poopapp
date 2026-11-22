using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Application;
using Trono.Modules.Identity.Domain;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class AccountSessions(
    IdentityDbContext database, UserManager<Account> users, IPasswordHasher<Account> hasher,
    DummyPasswordHash dummyHash, TimeProvider clock) : IAccountSessions
{
    private DateTimeOffset Now => new(clock.GetUtcNow().UtcTicks / 10 * 10, TimeSpan.Zero);

    public async Task<SessionTokens?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var normalized = users.NormalizeEmail(email);
        var account = await database.Users.FromSqlInterpolated(
            $"""SELECT * FROM identity."AspNetUsers" WHERE "NormalizedEmail" = {normalized} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null || await users.IsLockedOutAsync(account))
        {
            hasher.VerifyHashedPassword(account ?? new Account(), dummyHash.Value, password);
            return null;
        }

        if (!await users.CheckPasswordAsync(account, password))
        {
            EnsureSucceeded(await users.AccessFailedAsync(account));
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        EnsureSucceeded(await users.ResetAccessFailedCountAsync(account));
        var now = Now;
        var access = SessionCredentials.Create("at_");
        var refresh = SessionCredentials.Create("rt_");
        var session = new AuthSession(account.Id, account.SecurityStamp!, SessionCredentials.Hash(access), now);
        database.Set<AuthSession>().Add(session);
        database.Set<SessionRefreshToken>().Add(new SessionRefreshToken
        {
            SessionId = session.Id,
            Hash = SessionCredentials.Hash(refresh)
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Tokens(session, access, refresh, now);
    }

    public async Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!SessionCredentials.IsValid(refreshToken, "rt_")) return null;
        var hash = SessionCredentials.Hash(refreshToken);
        var sessionId = await database.Set<SessionRefreshToken>().AsNoTracking().Where(token => token.Hash == hash)
            .Select(token => (Guid?)token.SessionId).SingleOrDefaultAsync(cancellationToken);
        if (sessionId is null) return null;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var session = await database.Set<AuthSession>().FromSqlInterpolated(
            $"""SELECT * FROM identity."Sessions" WHERE "Id" = {sessionId.Value} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null) return null;
        var now = Now;
        var stamp = await database.Users.Where(account => account.Id == session.AccountId)
            .Select(account => account.SecurityStamp).SingleOrDefaultAsync(cancellationToken);
        if (!session.IsActive(now, stamp)) return null;
        var credential = await database.Set<SessionRefreshToken>().SingleAsync(token => token.Hash == hash, cancellationToken);
        if (credential.UsedAt is not null)
        {
            session.Revoke(now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var access = SessionCredentials.Create("at_");
        var refresh = SessionCredentials.Create("rt_");
        credential.UsedAt = now;
        session.RotateAccess(SessionCredentials.Hash(access), now);
        database.Set<SessionRefreshToken>().Add(new SessionRefreshToken
        {
            SessionId = session.Id,
            Hash = SessionCredentials.Hash(refresh)
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Tokens(session, access, refresh, now);
    }

    public async Task<SessionIdentity?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (!SessionCredentials.IsValid(accessToken, "at_")) return null;
        var hash = SessionCredentials.Hash(accessToken);
        var data = await database.Set<AuthSession>().AsNoTracking()
            .Join(database.Users, session => session.AccountId, account => account.Id,
                (session, account) => new { Session = session, account.SecurityStamp })
            .SingleOrDefaultAsync(value => value.Session.AccessTokenHash == hash, cancellationToken);
        return data is not null && data.Session.CanAuthenticate(Now, data.SecurityStamp)
            ? new SessionIdentity(data.Session.AccountId, data.Session.Id) : null;
    }

    public async Task RevokeAsync(SessionIdentity identity, CancellationToken cancellationToken)
    {
        var now = Now;
        await database.Set<AuthSession>()
            .Where(session => session.Id == identity.SessionId && session.AccountId == identity.AccountId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    public Task<OwnAccount?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().Where(account => account.Id == accountId)
            .Select(account => new OwnAccount(account.Id, account.Email!, account.EmailConfirmed, account.Nickname,
                account.PushNotificationsEnabled))
            .SingleOrDefaultAsync(cancellationToken);

    private static SessionTokens Tokens(AuthSession session, string access, string refresh, DateTimeOffset now) =>
        new(access, refresh, (int)(session.AccessExpiresAt - now).TotalSeconds, session.ExpiresAt);

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Não foi possível atualizar as credenciais da conta.");
    }
}
