using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using RegistrationDbContext = Trono.Modules.Identity.Infrastructure.IdentityDbContext;

namespace Trono.IntegrationTests;

public sealed class RegistrationDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithDatabase("trono_registration_tests")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = new RegistrationDbContext(new DbContextOptionsBuilder<RegistrationDbContext>()
            .UseNpgsql(ConnectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options);
        await context.Database.MigrateAsync();
        await using var records = new Trono.Modules.Records.Infrastructure.RecordsDbContext(
            new DbContextOptionsBuilder<Trono.Modules.Records.Infrastructure.RecordsDbContext>()
                .UseNpgsql(ConnectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "records")).Options);
        await records.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public async Task<long> CountAccountsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM identity.\"AspNetUsers\"", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

internal sealed class RegistrationApp(string connectionString, TimeProvider? clock = null, ILoggerProvider? logProvider = null,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?> { ["ConnectionStrings:Identity"] = connectionString };
            if (settings is not null) foreach (var setting in settings) values[setting.Key] = setting.Value;
            configuration.AddInMemoryCollection(values);
        });
        if (clock is not null) builder.ConfigureServices(services => services.AddSingleton(clock));
        if (logProvider is not null) builder.ConfigureLogging(logging => logging.AddProvider(logProvider));
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}

public sealed class RegistrationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Password = "uma senha longa para testes";
    private const string Route = "/api/v1/auth/register";

    [Fact]
    public async Task Registration_is_public_persists_a_hash_and_returns_only_an_opaque_id()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var email = NewEmail();

        using var response = await client.PostAsJsonAsync(Route, new { email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"PasswordHash\", \"EmailConfirmed\" FROM identity.\"AspNetUsers\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var hash = reader.GetString(0);
        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<IdentityUser<Guid>>().VerifyHashedPassword(new IdentityUser<Guid>(), hash, Password));
        Assert.False(reader.GetBoolean(1));
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData("email-invalido", Password)]
    [InlineData("Pessoa <pessoa@example.com>", Password)]
    [InlineData("pessoa nome@example.com", Password)]
    [InlineData("pessoa@example.com,outra@example.com", Password)]
    [InlineData("pessoa\r\n@example.com", Password)]
    [InlineData("pessoa@example.com", null)]
    [InlineData("pessoa@example.com", "")]
    [InlineData("pessoa@example.com", "12345678901")]
    [InlineData("pessoa@example.com", "            ")]
    public async Task Invalid_input_returns_problem_details_without_creating_accounts(string? email, string? password)
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var before = await database.CountAccountsAsync();
        using var response = await client.PostAsJsonAsync(Route, new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(json.RootElement.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await database.CountAccountsAsync());
    }

    [Fact]
    public async Task Oversized_fields_are_rejected_without_persisting_accounts()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var before = await database.CountAccountsAsync();
        using var response = await client.PostAsJsonAsync(Route, new
        {
            email = new string('a', 250) + "@example.com",
            password = new string('a', 129)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await database.CountAccountsAsync());
    }

    [Fact]
    public async Task Email_is_trimmed_and_unique_ignoring_case()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var email = NewEmail();
        using var first = await client.PostAsJsonAsync(Route, new { email = $"  {email}  ", password = Password });
        using var duplicate = await client.PostAsJsonAsync(Route, new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(email, await duplicate.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Concurrent_registration_creates_exactly_one_account()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var email = NewEmail();
        var before = await database.CountAccountsAsync();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(Route, new { email, password = Password }),
            client.PostAsJsonAsync(Route, new { email = email.ToUpperInvariant(), password = Password }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(before + 1, await database.CountAccountsAsync());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task Http_registration_is_rejected_before_creating_accounts()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
        var before = await database.CountAccountsAsync();
        using var response = await client.PostAsJsonAsync(Route, new { email = NewEmail(), password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await database.CountAccountsAsync());
    }

    [Fact]
    public async Task Registration_throttles_after_five_attempts_and_does_not_throttle_health()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync(Route, new { email = "invalid", password = Password });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var before = await database.CountAccountsAsync();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.5");
        using var blocked = await client.PostAsJsonAsync(Route, new { email = NewEmail(), password = Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await database.CountAccountsAsync());
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static string NewEmail() => $"pessoa-{Guid.NewGuid():N}@example.com";
}
