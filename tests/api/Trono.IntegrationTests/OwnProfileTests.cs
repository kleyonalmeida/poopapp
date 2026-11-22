using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class OwnProfileTransportTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET")]
    [InlineData("PATCH")]
    public async Task Profile_requires_authentication_and_https(string method)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/me/profile");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var insecure = new HttpRequestMessage(new HttpMethod(method), "http://localhost/api/v1/me/profile");
        using var rejected = await client.SendAsync(insecure);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }
}

public sealed class OwnProfileTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Route = "/api/v1/me/profile";
    private const string Password = "senha longa para teste de perfil";
    private sealed record LoggedIn(Guid Id, string AccessToken);

    [Fact]
    public async Task Changes_only_the_authenticated_profile_and_supports_clearing_and_deletion()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAndLoginAsync(client);
        var other = await RegisterAndLoginAsync(client);

        await AssertNicknameAsync(client, owner, Route, null);
        using var update = await SendAsync(client, owner, HttpMethod.Patch, $"{Route}?accountId={other.Id}",
            new { nickname = "  Guardiã do Trono 👑  ", accountId = other.Id });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Guardiã do Trono 👑", await ReadNicknameAsync(update, onlyNickname: true));
        Assert.True(update.Headers.CacheControl?.NoStore);
        await AssertNicknameAsync(client, owner, Route, "Guardiã do Trono 👑");
        await AssertNicknameAsync(client, owner, "/api/v1/me", "Guardiã do Trono 👑");
        await AssertNicknameAsync(client, other, Route, null);

        using var clear = await SendAsync(client, owner, HttpMethod.Patch, Route, new { nickname = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        await AssertNicknameAsync(client, owner, Route, null);

        using var save = await SendAsync(client, owner, HttpMethod.Patch, Route, new { nickname = "Antes da exclusão" });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var delete = await SendAsync(client, owner, HttpMethod.Delete, "/api/v1/me/account", new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var readAfterDelete = await SendAsync(client, owner, HttpMethod.Get, Route);
        Assert.Equal(HttpStatusCode.Unauthorized, readAfterDelete.StatusCode);
        await AssertNicknameAsync(client, other, Route, null);
    }

    [Fact]
    public async Task Invalid_values_do_not_overwrite_the_nickname()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await RegisterAndLoginAsync(client);
        using var save = await SendAsync(client, owner, HttpMethod.Patch, Route, new { nickname = new string('a', 40) });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        foreach (var invalid in new object[] { new { }, new { nickname = 3 }, new { nickname = " " },
                     new { nickname = "dois\nnomes" }, new { nickname = new string('a', 41) } })
        {
            using var response = await SendAsync(client, owner, HttpMethod.Patch, Route, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        await AssertNicknameAsync(client, owner, Route, new string('a', 40));
    }

    private static async Task<string?> ReadNicknameAsync(HttpResponseMessage response, bool onlyNickname)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (onlyNickname) Assert.Single(json.RootElement.EnumerateObject());
        return json.RootElement.GetProperty("nickname").GetString();
    }

    private static async Task AssertNicknameAsync(HttpClient client, LoggedIn owner, string path, string? nickname)
    {
        using var response = await SendAsync(client, owner, HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nickname, await ReadNicknameAsync(response, onlyNickname: path == Route));
    }

    private static async Task<LoggedIn> RegisterAndLoginAsync(HttpClient client)
    {
        var credentials = new { email = $"{Guid.NewGuid():N}@example.test", password = Password };
        using var created = await client.PostAsJsonAsync("/api/v1/auth/register", credentials);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var account = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return new LoggedIn(account.RootElement.GetProperty("id").GetGuid(), session.RootElement.GetProperty("accessToken").GetString()!);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, LoggedIn owner, HttpMethod method,
        string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
