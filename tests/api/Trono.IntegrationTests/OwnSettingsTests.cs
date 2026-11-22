using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class OwnSettingsTransportTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET")]
    [InlineData("PATCH")]
    public async Task Settings_require_a_session_over_https(string method)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var anonymous = new HttpRequestMessage(new HttpMethod(method), "/api/v1/me/settings");
        using var response = await client.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var insecure = new HttpRequestMessage(new HttpMethod(method), "http://localhost/api/v1/me/settings");
        using var rejected = await client.SendAsync(insecure);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }
}

public sealed class OwnSettingsTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Route = "/api/v1/me/settings";
    private const string Password = "senha longa para preferências";

    [Fact]
    public async Task Push_is_opt_in_and_only_the_owner_can_change_it()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var owner = app.CreateHttpsClient();
        using var other = app.CreateHttpsClient();
        await RegisterAndLoginAsync(owner);
        var otherId = await RegisterAndLoginAsync(other);

        Assert.False(await ReadPreferenceAsync(owner, Route, onlyPreference: true));
        using var enabled = await owner.PatchAsJsonAsync($"{Route}?accountId={otherId}",
            new { pushNotificationsEnabled = true, accountId = otherId });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.True(await ReadPreferenceAsync(enabled, onlyPreference: true));
        Assert.True(enabled.Headers.CacheControl?.NoStore);
        Assert.True(await ReadPreferenceAsync(owner, Route, onlyPreference: true));
        Assert.True(await ReadPreferenceAsync(owner, "/api/v1/me", onlyPreference: false));
        Assert.False(await ReadPreferenceAsync(other, Route, onlyPreference: true));

        foreach (var invalid in new object[] { new { }, new { pushNotificationsEnabled = (bool?)null },
                     new { pushNotificationsEnabled = "true" } })
        {
            using var response = await owner.PatchAsJsonAsync(Route, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.True(await ReadPreferenceAsync(owner, Route, onlyPreference: true));

        using var off = await owner.PatchAsJsonAsync(Route, new { pushNotificationsEnabled = false });
        using var repeated = await owner.PatchAsJsonAsync(Route, new { pushNotificationsEnabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.False(await ReadPreferenceAsync(owner, Route, onlyPreference: true));

        using var remove = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me/account")
        {
            Content = JsonContent.Create(new { password = Password })
        };
        using var deleted = await owner.SendAsync(remove);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var afterRemoval = await owner.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRemoval.StatusCode);
    }

    private static async Task<Guid> RegisterAndLoginAsync(HttpClient client)
    {
        var credentials = new { email = $"{Guid.NewGuid():N}@example.test", password = Password };
        using var created = await client.PostAsJsonAsync("/api/v1/auth/register", credentials);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = await created.Content.ReadFromJsonAsync<JsonElement>();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return account.GetProperty("id").GetGuid();
    }

    private static async Task<bool> ReadPreferenceAsync(HttpClient client, string path, bool onlyPreference)
    {
        using var response = await client.GetAsync(path);
        return await ReadPreferenceAsync(response, onlyPreference);
    }

    private static async Task<bool> ReadPreferenceAsync(HttpResponseMessage response, bool onlyPreference)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (onlyPreference) Assert.Single(body.EnumerateObject());
        return body.GetProperty("pushNotificationsEnabled").GetBoolean();
    }
}
