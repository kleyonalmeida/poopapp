namespace Trono.Modules.Identity.Application;

public sealed class LoginAccountValidator
{
    public IDictionary<string, string[]> Validate(LoginCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (!EmailInput.IsValid(command.Email?.Trim()))
            errors["email"] = ["Informe um e-mail válido com até 254 caracteres."];
        if (string.IsNullOrEmpty(command.Password) || command.Password.Length > 128)
            errors["password"] = ["Informe a senha, com até 128 caracteres."];
        return errors;
    }
}

public sealed class RefreshSessionValidator
{
    public IDictionary<string, string[]> Validate(string? refreshToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256)
            errors["refreshToken"] = ["Informe um refresh token válido."];
        return errors;
    }
}
