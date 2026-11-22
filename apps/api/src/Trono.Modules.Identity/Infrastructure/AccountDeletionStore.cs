using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class AccountDeletionStore(IdentityDbContext database, UserManager<Account> users) : IAccountDeletionStore
{
    public async Task<AccountDeletionStatus> DeleteAsync(Guid accountId, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var account = await database.Users.FromSqlInterpolated(
            $"""SELECT * FROM identity."AspNetUsers" WHERE "Id" = {accountId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null) return AccountDeletionStatus.SessionGone;
        if (await users.IsLockedOutAsync(account)) return AccountDeletionStatus.Denied;

        if (!await users.CheckPasswordAsync(account, password))
        {
            var failed = await users.AccessFailedAsync(account);
            if (!failed.Succeeded) return AccountDeletionStatus.Conflict;
            await transaction.CommitAsync(cancellationToken);
            return AccountDeletionStatus.Denied;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await users.DeleteAsync(account);
        if (!result.Succeeded) return AccountDeletionStatus.Conflict;
        // Existing database FKs cascade to sessions, refresh history and Identity dependents.
        await transaction.CommitAsync(cancellationToken);
        return AccountDeletionStatus.Deleted;
    }
}
