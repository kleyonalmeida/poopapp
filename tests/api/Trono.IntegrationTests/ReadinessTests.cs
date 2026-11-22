using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Trono.Modules.Identity.Infrastructure;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class ReadinessTests
{
    [Theory]
    [InlineData("")]
    [InlineData("segredo-de-fixture")]
    [InlineData("Host=127.0.0.1;Port=1;Database=fixture;Username=fixture;Timeout=1")]
    public async Task Unavailable_identity_blocks_readiness_but_preserves_liveness(string connectionString)
    {
        using var app = new RegistrationApp(connectionString);
        using var client = app.CreateClient();

        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");
        await AssertProbeAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"at_{new string('a', 64)}");
        await AssertProbeAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");
    }

    [Fact]
    public async Task Readiness_requires_all_identity_migrations_and_recovers_when_they_are_applied()
    {
        await using var postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
        await postgres.StartAsync();
        await VerifyMigrationStatesAsync(postgres.GetConnectionString());
    }

    private static async Task VerifyMigrationStatesAsync(string connectionString)
    {
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
        using var app = new RegistrationApp(connectionString);
        using var client = app.CreateHttpsClient();

        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");
        Assert.Empty(await database.Database.GetAppliedMigrationsAsync());

        await database.Database.MigrateAsync("AddAccountNickname");
        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");

        await database.Database.MigrateAsync();
        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy");
        await using var records = new Trono.Modules.Records.Infrastructure.RecordsDbContext(
            new DbContextOptionsBuilder<Trono.Modules.Records.Infrastructure.RecordsDbContext>()
                .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "records")).Options);
        await records.Database.MigrateAsync();
        await AssertProbeAsync(client, "/health/ready", HttpStatusCode.OK, "Healthy");
        await AssertProbeAsync(client, "/health/live", HttpStatusCode.OK, "Healthy");
    }

    private static async Task AssertProbeAsync(HttpClient client, string path, HttpStatusCode status, string body)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
