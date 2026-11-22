namespace Trono.Modules.Identity.Application;

public interface IAccountSettingsStore
{
    Task<bool> SetPushNotificationsAsync(Guid accountId, bool enabled, CancellationToken cancellationToken);
}

public sealed class UpdateOwnSettings(IAccountSettingsStore store)
{
    public Task<bool> ExecuteAsync(Guid accountId, bool pushNotificationsEnabled, CancellationToken cancellationToken) =>
        store.SetPushNotificationsAsync(accountId, pushNotificationsEnabled, cancellationToken);
}
