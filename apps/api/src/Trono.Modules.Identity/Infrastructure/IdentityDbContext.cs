using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Trono.Modules.Identity.Application;
using Trono.Modules.Identity.Domain;

namespace Trono.Modules.Identity.Infrastructure;

public sealed class Account : IdentityUser<Guid>
{
    public string? Nickname { get; set; }
    public bool PushNotificationsEnabled { get; set; }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserContext<Account, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.Entity<Account>().Property(account => account.Email).HasMaxLength(254).IsRequired();
        builder.Entity<Account>().Property(account => account.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Entity<Account>().HasIndex(account => account.NormalizedEmail).IsUnique();
        builder.Entity<Account>().Property(account => account.Nickname).HasMaxLength(NicknameValidator.MaxLength);
        builder.Entity<Account>().Property(account => account.PushNotificationsEnabled).HasDefaultValue(false);

        builder.Entity<AuthSession>(session =>
        {
            session.ToTable("Sessions");
            session.HasKey(value => value.Id);
            session.Property(value => value.AccessTokenHash).HasMaxLength(64).IsRequired();
            session.Property(value => value.SecurityStamp).HasMaxLength(256).IsRequired();
            session.HasIndex(value => value.AccessTokenHash).IsUnique();
            session.HasIndex(value => value.ExpiresAt);
            session.HasOne<Account>().WithMany().HasForeignKey(value => value.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SessionRefreshToken>(token =>
        {
            token.ToTable("SessionRefreshTokens");
            token.HasKey(value => value.Hash);
            token.Property(value => value.Hash).HasMaxLength(64);
            token.HasOne<AuthSession>().WithMany().HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
