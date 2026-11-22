using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Trono.Modules.Identity.Application;

internal static class EmailInput
{
    public static bool IsValid(string? email) =>
        !string.IsNullOrEmpty(email) && email.Length <= 254 && new EmailAddressAttribute().IsValid(email)
        && MailAddress.TryCreate(email, out var address) && address.Address == email;
}
