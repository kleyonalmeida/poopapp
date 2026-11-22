namespace Trono.Modules.Identity.Application;

public enum AccountDeletionStatus { Deleted, Invalid, Denied, SessionGone, Conflict }
public sealed record AccountDeletionResult(AccountDeletionStatus Status, IDictionary<string, string[]>? Errors = null);

public interface IAccountDeletionStore
{
    Task<AccountDeletionStatus> DeleteAsync(Guid accountId, string password, CancellationToken cancellationToken);
}

public sealed class AccountDeletionValidator
{
    public IDictionary<string, string[]> Validate(string? password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(password) || password.Length > 128)
            errors["password"] = ["Confirme sua senha, com até 128 caracteres."];
        return errors;
    }
}

public sealed class DeleteOwnAccount(IAccountDeletionStore store, AccountDeletionValidator validator)
{
    public async Task<AccountDeletionResult> ExecuteAsync(Guid accountId, string? password, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(password);
        if (errors.Count > 0) return new AccountDeletionResult(AccountDeletionStatus.Invalid, errors);
        return new AccountDeletionResult(await store.DeleteAsync(accountId, password!, cancellationToken));
    }
}
