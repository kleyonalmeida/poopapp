using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Trono.Modules.Identity.Domain;
using Trono.Modules.Identity.Infrastructure;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class SessionMigrationTests
{
    [Fact]
    public async Task Session_migration_upgrade_and_rollback_preserve_existing_accounts()
    {
        await using var postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
        await postgres.StartAsync();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), options => options.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
        var migrator = context.GetService<IMigrator>();
        const string initial = "20261112151932_InitialIdentity";
        await migrator.MigrateAsync(initial);
        var id = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."AspNetUsers"
                ("Id", "Email", "NormalizedEmail", "UserName", "NormalizedUserName", "SecurityStamp",
                 "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ({id}, 'existing@example.com', 'EXISTING@EXAMPLE.COM', 'existing@example.com',
                    'EXISTING@EXAMPLE.COM', 'existing-stamp', false, false, false, false, 0)
            """);

        await migrator.MigrateAsync();
        Assert.Equal(0, await context.Set<AuthSession>().CountAsync());
        var existing = await context.Users.AsNoTracking().SingleAsync(user => user.Id == id);
        Assert.Equal("existing-stamp", existing.SecurityStamp);
        Assert.Null(existing.Nickname);
        Assert.False(existing.PushNotificationsEnabled);
        await migrator.MigrateAsync(initial);
        var email = await context.Database.SqlQuery<string>(
            $"""SELECT "Email" AS "Value" FROM identity."AspNetUsers" WHERE "Id" = {id}""").SingleAsync();
        Assert.Equal("existing@example.com", email);
        await migrator.MigrateAsync();
        Assert.Equal(1, await context.Users.CountAsync());
    }
}
