using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class AccountDeletionTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Password = "  senha longa da conta de teste  ";
    private const string Route = "/api/v1/me/account";
    private sealed record Tokens(string AccessToken, string RefreshToken);

    [Fact]
    public async Task Delete_uses_the_owner_from_auth_removes_identity_data_and_invalidates_all_sessions()
    {
        using var logs = new SessionLogRecorder();
        using var app = new RegistrationApp(database.ConnectionString, logProvider: logs);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var other = await RegisterAsync(client);
        var first = await LoginAsync(client, owner.Email);
        var second = await LoginAsync(client, owner.Email);
        var otherSession = await LoginAsync(client, other.Email);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO identity."AspNetUserClaims" ("UserId", "ClaimType", "ClaimValue") VALUES (@id, 'fixture', 'fixture');
                INSERT INTO identity."AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value") VALUES (@id, 'fixture', 'fixture', 'fixture');
                INSERT INTO identity."AspNetUserLogins" ("UserId", "LoginProvider", "ProviderKey") VALUES (@id, 'fixture', @key);
                """, connection);
            command.Parameters.AddWithValue("id", owner.Id);
            command.Parameters.AddWithValue("key", Guid.NewGuid().ToString());
            await command.ExecuteNonQueryAsync();
        }

        // Neither body nor query is allowed to select another account.
        using var response = await DeleteAsync(client, first.AccessToken, Password, $"?userId={other.Id}", other.Id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await IdentityRowsAsync(owner.Id));
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM identity.\"SessionRefreshTokens\" WHERE \"Hash\" IN (@first, @second)", connection);
            command.Parameters.AddWithValue("first", Hash(first.RefreshToken));
            command.Parameters.AddWithValue("second", Hash(second.RefreshToken));
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
        foreach (var tokens in new[] { first, second })
        {
            using var me = await GetMeAsync(client, tokens.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
            using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { tokens.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var otherMe = await GetMeAsync(client, otherSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, otherMe.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { owner.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var recreated = await client.PostAsJsonAsync("/api/v1/auth/register", new { owner.Email, password = Password });
        Assert.Equal(HttpStatusCode.Created, recreated.StatusCode);
        using var newJson = JsonDocument.Parse(await recreated.Content.ReadAsStringAsync());
        Assert.NotEqual(owner.Id, newJson.RootElement.GetProperty("id").GetGuid());
        using var oldAccess = await GetMeAsync(client, first.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldAccess.StatusCode);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(Password, StringComparison.Ordinal)
            || message.Contains(first.AccessToken, StringComparison.Ordinal) || message.Contains(first.RefreshToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Anonymous_deletion_is_rejected()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        using var response = await DeleteAsync(client, null, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Password_is_required_without_deleting_the_account(string? password)
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var tokens = await LoginAsync(client, owner.Email);
        using var response = await DeleteAsync(client, tokens.AccessToken, password);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(await IdentityRowsAsync(owner.Id) > 0);
    }

    [Fact]
    public async Task Wrong_password_does_not_delete_or_revoke_the_current_session()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var tokens = await LoginAsync(client, owner.Email);
        using var response = await DeleteAsync(client, tokens.AccessToken, Password.Trim());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.Email, body);
        Assert.DoesNotContain(Password, body);
        using var me = await GetMeAsync(client, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Locked_account_cannot_be_deleted_even_with_the_correct_password()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var tokens = await LoginAsync(client, owner.Email);
        for (var i = 0; i < 5; i++)
        {
            using var failure = await client.PostAsJsonAsync("/api/v1/auth/login", new { owner.Email, password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        using var response = await DeleteAsync(client, tokens.AccessToken, Password);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await IdentityRowsAsync(owner.Id) > 0);
    }

    [Fact]
    public async Task Duplicate_concurrent_deletion_has_one_effect_and_no_server_error()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var tokens = await LoginAsync(client, owner.Email);
        var responses = await Task.WhenAll(DeleteAsync(client, tokens.AccessToken, Password), DeleteAsync(client, tokens.AccessToken, Password));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
            Assert.Equal(0, await IdentityRowsAsync(owner.Id));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Deletion_also_invalidates_a_different_session_refreshing_concurrently()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var first = await LoginAsync(client, owner.Email);
        var other = await LoginAsync(client, owner.Email);
        var responses = await Task.WhenAll(DeleteAsync(client, first.AccessToken, Password),
            client.PostAsJsonAsync("/api/v1/auth/refresh", new { other.RefreshToken }));
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, responses[0].StatusCode);
            Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
            if (responses[1].StatusCode == HttpStatusCode.OK)
            {
                var renewed = (await responses[1].Content.ReadFromJsonAsync<Tokens>())!;
                using var me = await GetMeAsync(client, renewed.AccessToken);
                Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
                using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { renewed.RefreshToken });
                Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
            }
            Assert.Equal(0, await IdentityRowsAsync(owner.Id));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Http_deletion_is_rejected_and_attempts_are_throttled_independently()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAsync(client);
        var tokens = await LoginAsync(client, owner.Email);
        using var insecure = new HttpRequestMessage(HttpMethod.Delete, "http://localhost" + Route) { Content = JsonContent.Create(new { password = Password }) };
        insecure.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var http = await client.SendAsync(insecure);
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
        for (var i = 0; i < 5; i++)
        {
            using var invalid = await DeleteAsync(client, tokens.AccessToken, null);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.3");
        using var blocked = await DeleteAsync(client, tokens.AccessToken, Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(await IdentityRowsAsync(owner.Id) > 0);
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private async Task<long> IdentityRowsAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM identity."AspNetUsers" WHERE "Id" = @id)
              + (SELECT count(*) FROM identity."Sessions" WHERE "AccountId" = @id)
              + (SELECT count(*) FROM identity."AspNetUserClaims" WHERE "UserId" = @id)
              + (SELECT count(*) FROM identity."AspNetUserLogins" WHERE "UserId" = @id)
              + (SELECT count(*) FROM identity."AspNetUserTokens" WHERE "UserId" = @id)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }
    private static async Task<(Guid Id, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), email);
    }
    private static async Task<Tokens> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Tokens>())!;
    }
    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string? access, string? password, string suffix = "", Guid? accountId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Route + suffix) { Content = JsonContent.Create(new { password, accountId }) };
        if (access is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string access)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await client.SendAsync(request);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
