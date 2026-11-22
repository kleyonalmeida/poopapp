using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Trono.IntegrationTests;

public sealed class RecordTransportTests(WebApplicationFactory<Program> app) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET", "/api/v1/records")]
    [InlineData("POST", "/api/v1/records")]
    [InlineData("GET", "/api/v1/records/11111111-1111-1111-1111-111111111111")]
    [InlineData("PATCH", "/api/v1/records/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/v1/records/11111111-1111-1111-1111-111111111111")]
    public async Task Records_require_https_and_authentication(string method, string path)
    {
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var insecure = new HttpRequestMessage(new HttpMethod(method), "http://localhost" + path);
        using var rejected = await client.SendAsync(insecure);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }
}

public sealed class RecordTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    private const string Route = "/api/v1/records";
    private const string Password = "senha longa para registro privado";
    private sealed record Owner(Guid Id, string Access);
    private static object Content(DateTimeOffset? at = null, int? duration = 120, string? type = "torazona", string? notes = "  Uma história privada  ") =>
        new { occurredAt = at ?? DateTimeOffset.UtcNow.AddMinutes(-10), durationSeconds = duration, type, notes };

    [Fact]
    public async Task Owner_can_create_read_edit_and_delete_a_private_record_but_another_account_cannot()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await Login(client);
        var other = await Login(client);
        using var created = await Send(client, owner, "POST", Route + $"?accountId={other.Id}", Content(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        using var data = await Json(created);
        var id = data.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Uma história privada", data.RootElement.GetProperty("notes").GetString());
        Assert.Equal(new[] { "durationSeconds", "id", "notes", "occurredAt", "type" },
            data.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(Route + "/" + id, created.Headers.Location?.OriginalString);
        using var read = await Send(client, owner, "GET", Route + "/" + id);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var invalidEdit = await Send(client, owner, "PATCH", Route + "/" + id, Content(duration: -1));
        Assert.Equal(HttpStatusCode.BadRequest, invalidEdit.StatusCode);
        using var unchanged = await Send(client, owner, "GET", Route + "/" + id);
        using var unchangedJson = await Json(unchanged);
        Assert.Equal("Uma história privada", unchangedJson.RootElement.GetProperty("notes").GetString());
        foreach (var method in new[] { "GET", "PATCH", "DELETE" })
        {
            using var denied = await Send(client, other, method, Route + "/" + id, method == "PATCH" ? Content() : null);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.DoesNotContain("história", await denied.Content.ReadAsStringAsync());
        }
        using var otherList = await Send(client, other, "GET", Route);
        using var otherJson = await Json(otherList);
        Assert.Empty(otherJson.RootElement.GetProperty("items").EnumerateArray());
        using var edit = await Send(client, owner, "PATCH", Route + "/" + id, Content(duration: null, type: null, notes: null));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using var edited = await Json(edit);
        Assert.Equal(JsonValueKind.Null, edited.RootElement.GetProperty("durationSeconds").ValueKind);
        Assert.Equal(JsonValueKind.Null, edited.RootElement.GetProperty("type").ValueKind);
        Assert.Equal(JsonValueKind.Null, edited.RootElement.GetProperty("notes").ValueKind);
        using var delete = await Send(client, owner, "DELETE", Route + "/" + id);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missing = await Send(client, owner, "GET", Route + "/" + id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Concurrent_account_deletion_leaves_no_orphan_records_or_private_logs()
    {
        using var logs = new SessionLogRecorder();
        using var app = new RegistrationApp(database.ConnectionString, logProvider: logs);
        using var client = app.CreateHttpsClient();
        var owner = await Login(client);
        var note = "historia-sigilosa-" + Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(
            Send(client, owner, "POST", Route, Content(notes: note), Guid.NewGuid()),
            Send(client, owner, "DELETE", "/api/v1/me/account", new { password = Password }));
        try
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Unauthorized });
            Assert.Equal(HttpStatusCode.NoContent, results[1].StatusCode);
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM records.\"Records\" WHERE \"AccountId\" = @owner", connection);
            query.Parameters.AddWithValue("owner", owner.Id);
            Assert.Equal(0L, await query.ExecuteScalarAsync());
            var captured = string.Join('\n', logs.Messages);
            Assert.DoesNotContain(note, captured);
            Assert.DoesNotContain(owner.Access, captured);
            Assert.DoesNotContain(Password, captured);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task Invalid_input_and_pagination_are_rejected_without_creating_records()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await Login(client);
        foreach (var invalid in new object[] {
            new { }, Content(DateTimeOffset.UtcNow.AddDays(-31)), Content(DateTimeOffset.UtcNow.AddMinutes(6)),
            Content(duration: 0), Content(duration: 86401), Content(type: "desconhecido"), Content(notes: new string('a', 281)),
            new { occurredAt = "2026-01-01T12:00:00", durationSeconds = (int?)null, type = (string?)null, notes = (string?)null } })
        {
            using var response = await Send(client, owner, "POST", Route, invalid, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        using var noKey = await Send(client, owner, "POST", Route, Content());
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        foreach (var query in new[] { "?offset=-1", "?limit=0", "?limit=51", "?offset=abc" })
        {
            using var response = await Send(client, owner, "GET", Route + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var list = await Send(client, owner, "GET", Route);
        using var json = await Json(list);
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_replays_create_once_and_reusing_a_key_with_other_content_is_a_conflict()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await Login(client);
        var key = Guid.NewGuid();
        var content = Content();
        var results = await Task.WhenAll(Send(client, owner, "POST", Route, content, key), Send(client, owner, "POST", Route, content, key));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            using var first = await Json(results[0]);
            using var second = await Json(results[1]);
            var id = first.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(id, second.RootElement.GetProperty("id").GetGuid());
            using var conflicting = await Send(client, owner, "POST", Route, Content(notes: "outra história"), key);
            Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
            using var deleted = await Send(client, owner, "DELETE", Route + "/" + id);
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var replay = await Send(client, owner, "POST", Route, content, key);
            Assert.Equal(HttpStatusCode.Gone, replay.StatusCode);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task History_is_paginated_and_account_deletion_removes_records_and_deduplication_data()
    {
        using var app = new RegistrationApp(database.ConnectionString);
        using var client = app.CreateHttpsClient();
        var owner = await Login(client);
        var other = await Login(client);
        for (var i = 0; i < 3; i++)
        {
            using var created = await Send(client, owner, "POST", Route, Content(DateTimeOffset.UtcNow.AddHours(-i - 1)), Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        using var others = await Send(client, other, "POST", Route, Content(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, others.StatusCode);
        using var first = await Send(client, owner, "GET", Route + "?limit=2");
        using var page = await Json(first);
        Assert.Equal(2, page.RootElement.GetProperty("items").GetArrayLength());
        Assert.True(page.RootElement.GetProperty("hasMore").GetBoolean());
        using var next = await Send(client, owner, "GET", Route + "?offset=2&limit=2");
        using var tail = await Json(next);
        Assert.Single(tail.RootElement.GetProperty("items").EnumerateArray());
        Assert.False(tail.RootElement.GetProperty("hasMore").GetBoolean());
        using var deletion = await Send(client, owner, "DELETE", "/api/v1/me/account", new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        foreach (var table in new[] { "Records", "CreationReceipts" })
        {
            await using var query = new NpgsqlCommand($"SELECT count(*) FROM records.\"{table}\" WHERE \"AccountId\" = @owner", connection);
            query.Parameters.AddWithValue("owner", owner.Id);
            Assert.Equal(0L, await query.ExecuteScalarAsync());
        }
        using var otherList = await Send(client, other, "GET", Route);
        using var otherPage = await Json(otherList);
        Assert.Single(otherPage.RootElement.GetProperty("items").EnumerateArray());
    }

    private static async Task<Owner> Login(HttpClient client)
    {
        var credentials = new { email = $"record-{Guid.NewGuid():N}@example.test", password = Password };
        using var created = await client.PostAsJsonAsync("/api/v1/auth/register", credentials);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var account = await Json(created);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await Json(login);
        return new Owner(account.RootElement.GetProperty("id").GetGuid(), session.RootElement.GetProperty("accessToken").GetString()!);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, Owner owner, string method, string path, object? body = null, Guid? key = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Access);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.Add("Idempotency-Key", key.ToString());
        return await client.SendAsync(request);
    }
    private static async Task<JsonDocument> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
