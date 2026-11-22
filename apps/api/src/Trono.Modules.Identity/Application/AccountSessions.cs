namespace Trono.Modules.Identity.Application;

public sealed record SessionTokens(string AccessToken, string RefreshToken, int ExpiresIn, DateTimeOffset RefreshExpiresAt);
public sealed record SessionIdentity(Guid AccountId, Guid SessionId);
public sealed record OwnAccount(Guid Id, string Email, bool EmailConfirmed, string? Nickname = null,
    bool PushNotificationsEnabled = false);

public interface IAccountSessions
{
    Task<SessionTokens?> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task<SessionTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<SessionIdentity?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken);
    Task RevokeAsync(SessionIdentity identity, CancellationToken cancellationToken);
    Task<OwnAccount?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed record LoginCommand(string? Email, string? Password);
public sealed record SessionResult(SessionTokens? Tokens, IDictionary<string, string[]>? Errors = null);

public sealed class LoginAccount(IAccountSessions sessions, LoginAccountValidator validator)
{
    public async Task<SessionResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(command);
        if (errors.Count > 0) return new SessionResult(null, errors);
        return new SessionResult(await sessions.LoginAsync(command.Email!.Trim(), command.Password!, cancellationToken));
    }
}

public sealed class RefreshSession(IAccountSessions sessions, RefreshSessionValidator validator)
{
    public async Task<SessionResult> ExecuteAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(refreshToken);
        if (errors.Count > 0) return new SessionResult(null, errors);
        return new SessionResult(await sessions.RefreshAsync(refreshToken!, cancellationToken));
    }
}
