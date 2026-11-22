namespace Trono.Modules.Identity.Application;

public sealed class NicknameValidator
{
    public const int MaxLength = 40;

    public IDictionary<string, string[]> Validate(string? nickname)
    {
        var errors = new Dictionary<string, string[]>();
        if (nickname is not null && (nickname.Length is < 1 or > MaxLength
            || nickname.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029')))
        {
            errors["nickname"] = ["Informe um apelido de 1 a 40 caracteres em uma única linha ou use null para removê-lo."];
        }

        return errors;
    }
}
