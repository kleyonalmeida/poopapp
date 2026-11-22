using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class AccountProfileStore(IdentityDbContext database) : IAccountProfileStore
{
    public async Task<bool> SetNicknameAsync(Guid accountId, string? nickname, CancellationToken cancellationToken) =>
        await database.Users.Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(update => update.SetProperty(account => account.Nickname, nickname), cancellationToken) == 1;
}
