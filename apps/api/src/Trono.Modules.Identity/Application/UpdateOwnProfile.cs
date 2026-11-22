namespace Trono.Modules.Identity.Application;

public interface IAccountProfileStore
{
    Task<bool> SetNicknameAsync(Guid accountId, string? nickname, CancellationToken cancellationToken);
}

public sealed record OwnProfile(string? Nickname);
public sealed record UpdateOwnProfileResult(OwnProfile? Profile, IDictionary<string, string[]>? Errors = null);

public sealed class UpdateOwnProfile(IAccountProfileStore store, NicknameValidator validator)
{
    public async Task<UpdateOwnProfileResult> ExecuteAsync(Guid accountId, string? nickname, CancellationToken cancellationToken)
    {
        var normalized = nickname?.Trim();
        var errors = validator.Validate(normalized);
        if (errors.Count > 0) return new UpdateOwnProfileResult(null, errors);

        var updated = await store.SetNicknameAsync(accountId, normalized, cancellationToken);
        return new UpdateOwnProfileResult(updated ? new OwnProfile(normalized) : null);
    }
}
