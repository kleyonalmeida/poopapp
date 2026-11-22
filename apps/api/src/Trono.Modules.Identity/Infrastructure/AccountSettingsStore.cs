using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class AccountSettingsStore(IdentityDbContext database) : IAccountSettingsStore
{
    public async Task<bool> SetPushNotificationsAsync(Guid accountId, bool enabled, CancellationToken cancellationToken) =>
        await database.Users.Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(update => update.SetProperty(account => account.PushNotificationsEnabled, enabled), cancellationToken) == 1;
}
