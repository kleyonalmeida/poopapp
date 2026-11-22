using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class AccountRegistrationStore(UserManager<Account> users) : IAccountRegistrationStore
{
    public async Task<RegistrationResult> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = new Account { Id = Guid.NewGuid(), UserName = email, Email = email };
        try
        {
            var result = await users.CreateAsync(account, password);
            if (result.Succeeded)
                return new RegistrationResult(RegistrationStatus.Created, account.Id);

            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                return new RegistrationResult(RegistrationStatus.Duplicate);

            return new RegistrationResult(RegistrationStatus.Invalid, Errors:
                new Dictionary<string, string[]> { ["account"] = ["Verifique os dados informados."] });
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            return new RegistrationResult(RegistrationStatus.Duplicate);
        }
    }
}
