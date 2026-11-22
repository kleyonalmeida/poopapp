namespace Trono.Modules.Identity.Domain;

public sealed class AuthSession
{
    public const int AccessLifetimeSeconds = 15 * 60;
    private AuthSession() { }

    public AuthSession(Guid accountId, string securityStamp, string accessHash, DateTimeOffset now)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Conta obrigatória.", nameof(accountId));
        ArgumentException.ThrowIfNullOrEmpty(securityStamp);
        ArgumentException.ThrowIfNullOrEmpty(accessHash);
        Id = Guid.NewGuid();
        AccountId = accountId;
        SecurityStamp = securityStamp;
        CreatedAt = now.ToUniversalTime();
        ExpiresAt = CreatedAt.AddDays(7);
        RotateAccess(accessHash, CreatedAt);
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string SecurityStamp { get; private set; } = null!;
    public string AccessTokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset AccessExpiresAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive(DateTimeOffset now, string? securityStamp) =>
        RevokedAt is null && now < ExpiresAt && SecurityStamp == securityStamp;

    public bool CanAuthenticate(DateTimeOffset now, string? securityStamp) =>
        IsActive(now, securityStamp) && now < AccessExpiresAt;

    public void RotateAccess(string accessHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessHash);
        if (!IsActive(now, SecurityStamp)) throw new InvalidOperationException("Sessão inativa.");
        AccessTokenHash = accessHash;
        var desiredExpiry = now.AddSeconds(AccessLifetimeSeconds);
        AccessExpiresAt = desiredExpiry < ExpiresAt ? desiredExpiry : ExpiresAt;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now.ToUniversalTime();
}
