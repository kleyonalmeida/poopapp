using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trono.Modules.Identity.Infrastructure;
using Xunit;

namespace Trono.IntegrationTests;

internal sealed class SessionClock : TimeProvider
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
}

internal sealed class SessionLogRecorder : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Recorder(Messages);
    public void Dispose() { }
    private sealed class Recorder(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Enqueue(formatter(state, exception));
    }
}

public sealed class SessionTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Password = "  uma senha longa e privada  ";
    private const string LoginRoute = "/api/v1/auth/login";
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private const string LogoutRoute = "/api/v1/auth/logout";
    private const string MeRoute = "/api/v1/me";

    private sealed record Tokens(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken, DateTimeOffset RefreshExpiresAt);
    private sealed record RegisteredAccount(Guid Id, string Email);

    [Fact]
    public async Task Login_persists_only_token_hashes_and_returns_a_private_account_view()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, $"  {account.Email.ToUpperInvariant()}  ");
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(900, tokens.ExpiresIn);
        Assert.NotEqual(tokens.AccessToken, tokens.RefreshToken);

        using var me = await GetMeAsync(client, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("no-store", me.Headers.CacheControl?.ToString());
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(account.Id, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(account.Email, json.RootElement.GetProperty("email").GetString());
        Assert.False(json.RootElement.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("nickname").ValueKind);
        Assert.False(json.RootElement.GetProperty("pushNotificationsEnabled").GetBoolean());
        Assert.Equal(new[] { "email", "emailConfirmed", "id", "nickname", "pushNotificationsEnabled" },
            json.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT s.\"AccessTokenHash\", r.\"Hash\" FROM identity.\"Sessions\" s JOIN identity.\"SessionRefreshTokens\" r ON r.\"SessionId\" = s.\"Id\" WHERE s.\"AccountId\" = @id", connection);
        command.Parameters.AddWithValue("id", account.Id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(Hash(tokens.AccessToken), reader.GetString(0));
        Assert.Equal(Hash(tokens.RefreshToken), reader.GetString(1));
    }

    [Fact]
    public async Task Account_data_comes_from_the_authenticated_user_even_with_a_foreign_id_in_query()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var first = await RegisterAsync(client);
        var other = await RegisterAsync(client);
        var firstSession = await LoginAsync(client, first.Email);
        var otherSession = await LoginAsync(client, other.Email);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{MeRoute}?userId={other.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstSession.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(first.Email, body);
        Assert.DoesNotContain(other.Email, body);
        using var otherMe = await GetMeAsync(client, otherSession.AccessToken);
        Assert.Contains(other.Email, await otherMe.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_user_wrong_password_and_trimmed_password_have_the_same_generic_failure()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        string? title = null;
        foreach (var input in new[] { (account.Email, "errada"), ($"missing-{Guid.NewGuid():N}@example.com", Password), (account.Email, Password.Trim()) })
        {
            using var response = await client.PostAsJsonAsync(LoginRoute, new { email = input.Item1, password = input.Item2 });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            title ??= json.RootElement.GetProperty("title").GetString();
            Assert.Equal(title, json.RootElement.GetProperty("title").GetString());
            Assert.DoesNotContain(account.Email, body);
            Assert.DoesNotContain(Password, body);
        }
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("invalid", Password)]
    [InlineData("pessoa@example.com", null)]
    [InlineData("pessoa@example.com", "")]
    public async Task Login_validates_required_fields(string? email, string? password)
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync(LoginRoute, new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("GET", MeRoute)]
    [InlineData("POST", LogoutRoute)]
    public async Task Private_operations_require_authentication(string method, string route)
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task Token_types_tampering_and_query_tokens_cannot_bypass_authentication()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, account.Email);
        foreach (var value in new[] { tokens.RefreshToken, tokens.AccessToken + "x", "at_" + new string('A', 64) })
        {
            using var response = await GetMeAsync(client, value);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var swapped = await client.PostAsJsonAsync(RefreshRoute, new { refreshToken = tokens.AccessToken });
        Assert.Equal(HttpStatusCode.Unauthorized, swapped.StatusCode);
        using var query = await client.GetAsync($"{MeRoute}?access_token={tokens.AccessToken}");
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_both_tokens_of_only_the_current_session()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var current = await LoginAsync(client, account.Email);
        var other = await LoginAsync(client, account.Email);
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using var logout = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var me = await GetMeAsync(client, current.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var refresh = await client.PostAsJsonAsync(RefreshRoute, new { current.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var otherMe = await GetMeAsync(client, other.AccessToken);
        Assert.Equal(HttpStatusCode.OK, otherMe.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_both_tokens_and_replay_revokes_the_whole_session()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var original = await LoginAsync(client, account.Email);
        using var response = await client.PostAsJsonAsync(RefreshRoute, new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = (await response.Content.ReadFromJsonAsync<Tokens>())!;
        Assert.NotEqual(original.AccessToken, renewed.AccessToken);
        Assert.NotEqual(original.RefreshToken, renewed.RefreshToken);
        Assert.Equal(original.RefreshExpiresAt, renewed.RefreshExpiresAt);
        using var oldMe = await GetMeAsync(client, original.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldMe.StatusCode);
        using var newMe = await GetMeAsync(client, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, newMe.StatusCode);

        using var replay = await client.PostAsJsonAsync(RefreshRoute, new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var revoked = await GetMeAsync(client, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var next = await client.PostAsJsonAsync(RefreshRoute, new { renewed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refresh_has_one_winner_and_reuse_revokes_its_result()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var original = await LoginAsync(client, account.Email);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(RefreshRoute, new { original.RefreshToken }),
            client.PostAsJsonAsync(RefreshRoute, new { original.RefreshToken }));
        try
        {
            var winner = Assert.Single(responses, result => result.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, result => result.StatusCode == HttpStatusCode.Unauthorized);
            var tokens = (await winner.Content.ReadFromJsonAsync<Tokens>())!;
            using var me = await GetMeAsync(client, tokens.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task Access_and_refresh_expire_at_their_exact_boundaries()
    {
        var clock = new SessionClock();
        using var app = new RegistrationApp(database.ConnectionString, clock);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, account.Email);
        clock.Advance(TimeSpan.FromMinutes(15));
        using var expired = await GetMeAsync(client, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var refresh = await client.PostAsJsonAsync(RefreshRoute, new { tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = (await refresh.Content.ReadFromJsonAsync<Tokens>())!;
        clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(15));
        using var expiredRefresh = await client.PostAsJsonAsync(RefreshRoute, new { renewed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, expiredRefresh.StatusCode);
        using var expiredMe = await GetMeAsync(client, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredMe.StatusCode);
    }

    [Fact]
    public async Task Refresh_near_the_deadline_never_extends_the_absolute_session_lifetime()
    {
        var clock = new SessionClock();
        using var app = new RegistrationApp(database.ConnectionString, clock);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, account.Email);
        clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        using var response = await client.PostAsJsonAsync(RefreshRoute, new { tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = (await response.Content.ReadFromJsonAsync<Tokens>())!;
        Assert.InRange(renewed.ExpiresIn, 59, 60);
        Assert.Equal(tokens.RefreshExpiresAt, renewed.RefreshExpiresAt);
        clock.Advance(TimeSpan.FromMinutes(1));
        using var me = await GetMeAsync(client, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Updating_the_security_stamp_invalidates_access_and_refresh()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, account.Email);
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var user = (await manager.FindByEmailAsync(account.Email))!;
        Assert.True((await manager.UpdateSecurityStampAsync(user)).Succeeded);
        using var me = await GetMeAsync(client, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var refresh = await client.PostAsJsonAsync(RefreshRoute, new { tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Five_concurrent_wrong_passwords_lock_the_account()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var failures = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync(LoginRoute, new { account.Email, password = "incorrect" })));
        foreach (var failure in failures)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
            failure.Dispose();
        }
        using var locked = await client.PostAsJsonAsync(LoginRoute, new { account.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        Assert.True(await manager.IsLockedOutAsync((await manager.FindByEmailAsync(account.Email))!));
    }

    [Fact]
    public async Task Login_throttles_after_ten_attempts_without_blocking_registration_or_health()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        for (var i = 0; i < 10; i++)
        {
            using var response = await client.PostAsJsonAsync(LoginRoute, new { email = "unknown@example.com", password = Password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.8");
        using var blocked = await client.PostAsJsonAsync(LoginRoute, new { email = "unknown@example.com", password = Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        await RegisterAsync(client);
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Refresh_has_its_own_limit_and_rejects_missing_input()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        using var missing = await client.PostAsJsonAsync(RefreshRoute, new { refreshToken = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        for (var i = 1; i < 30; i++)
        {
            using var response = await client.PostAsJsonAsync(RefreshRoute, new { refreshToken = "rt_" + new string('A', 64) });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var blocked = await client.PostAsJsonAsync(RefreshRoute, new { refreshToken = "unknown" });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Theory]
    [InlineData("POST", LoginRoute)]
    [InlineData("POST", RefreshRoute)]
    [InlineData("POST", LogoutRoute)]
    [InlineData("GET", MeRoute)]
    public async Task Session_operations_reject_http(string method, string route)
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "http://localhost" + route);
        request.Content = JsonContent.Create(new { email = "fixture@example.com", password = Password, refreshToken = "unknown" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Credentials_do_not_appear_in_application_logs_even_when_query_tokens_are_rejected()
    {
        using var logs = new SessionLogRecorder();
        using var app = new RegistrationApp(database.ConnectionString, logProvider: logs);
        using var client = app.CreateHttpsClient();
        var account = await RegisterAsync(client);
        var tokens = await LoginAsync(client, account.Email);
        using var me = await GetMeAsync(client, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var query = await client.GetAsync($"{MeRoute}?access_token={tokens.AccessToken}");
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(Password, StringComparison.Ordinal)
            || message.Contains(tokens.AccessToken, StringComparison.Ordinal)
            || message.Contains(tokens.RefreshToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sessions_survive_recreating_the_application_host()
    {
        Tokens tokens;
        using (var first = new RegistrationApp(database.ConnectionString))
        using (var client = first.CreateHttpsClient())
        {
            var account = await RegisterAsync(client);
            tokens = await LoginAsync(client, account.Email);
        }
        using var restarted = new RegistrationApp(database.ConnectionString);
        using var second = restarted.CreateHttpsClient();
        using var me = await GetMeAsync(second, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    private static async Task<RegisteredAccount> RegisterAsync(HttpClient client)
    {
        var email = $"session-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new RegisteredAccount(json.RootElement.GetProperty("id").GetGuid(), email);
    }

    private static async Task<Tokens> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(LoginRoute, new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        return (await response.Content.ReadFromJsonAsync<Tokens>())!;
    }

    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MeRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
