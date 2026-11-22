using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Trono.Modules.Identity.Infrastructure;

internal static class SessionCredentials
{
    public static string Create(string prefix) => prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool IsValid(string? value, string prefix) => value is { Length: 67 }
        && value.StartsWith(prefix, StringComparison.Ordinal) && value.AsSpan(3).IndexOfAnyExcept("0123456789ABCDEFabcdef") < 0;
}

internal sealed class DummyPasswordHash(IOptions<PasswordHasherOptions> options)
{
    public string Value { get; } = new PasswordHasher<Account>(options)
        .HashPassword(new Account(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
}

public sealed class SessionRefreshToken
{
    public string Hash { get; set; } = null!;
    public Guid SessionId { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
