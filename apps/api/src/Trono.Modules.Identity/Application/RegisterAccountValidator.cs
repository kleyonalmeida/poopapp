namespace Trono.Modules.Identity.Application;

public sealed class RegisterAccountValidator
{
    public IDictionary<string, string[]> Validate(RegisterAccountCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        var email = command.Email?.Trim();
        if (!EmailInput.IsValid(email))
            errors["email"] = ["Informe um e-mail válido com até 254 caracteres."];

        if (command.Password is null || command.Password.Length is < 12 or > 128)
            errors["password"] = ["A senha deve ter entre 12 e 128 caracteres."];

        return errors;
    }
}
