namespace Trono.Modules.Identity.Application;

public sealed record RegisterAccountCommand(string? Email, string? Password);

public enum RegistrationStatus { Created, Invalid, Duplicate }

public sealed record RegistrationResult(
    RegistrationStatus Status,
    Guid? Id = null,
    IDictionary<string, string[]>? Errors = null);

public interface IAccountRegistrationStore
{
    Task<RegistrationResult> CreateAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed class RegisterAccount(IAccountRegistrationStore store, RegisterAccountValidator validator)
{
    public Task<RegistrationResult> ExecuteAsync(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(command);
        if (errors.Count > 0)
            return Task.FromResult(new RegistrationResult(RegistrationStatus.Invalid, Errors: errors));

        return store.CreateAsync(command.Email!.Trim(), command.Password!, cancellationToken);
    }
}
