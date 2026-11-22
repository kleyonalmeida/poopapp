using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Trono.Modules.Identity.Application;
using Trono.Modules.Identity.Domain;
using Trono.Modules.Identity.Infrastructure;
using Xunit;

namespace Trono.IntegrationTests;

internal sealed class RetentionClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class SessionCleanupTests : IAsyncLifetime
{
    private readonly RegistrationDatabase _database = new();
    private readonly DateTimeOffset _now = new(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero);
    public Task InitializeAsync() => _database.InitializeAsync();
    public Task DisposeAsync() => _database.DisposeAsync();

    private IdentityDbContext Context() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(_database.ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);

    private async Task<AuthSession> SeedAsync(DateTimeOffset expiry, bool revoked = false, bool consumedRefresh = false)
    {
        await using var context = Context();
        var accountId = Guid.NewGuid();
        var email = $"retention-{accountId:N}@example.com";
        context.Users.Add(new Account { Id = accountId, Email = email, NormalizedEmail = email.ToUpperInvariant(), SecurityStamp = "fixture" });
        var hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var session = new AuthSession(accountId, "fixture", hash, expiry.AddDays(-7));
        if (revoked) session.Revoke(_now.AddMinutes(-1));
        context.Set<AuthSession>().Add(session);
        context.Set<SessionRefreshToken>().Add(new SessionRefreshToken
        {
            Hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            SessionId = session.Id,
            UsedAt = consumedRefresh ? _now.AddMinutes(-1) : null
        });
        await context.SaveChangesAsync();
        return session;
    }

    private async Task<int> PruneAsync(int size = 100)
    {
        using var app = new RegistrationApp(_database.ConnectionString, new RetentionClock(_now));
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PruneExpiredSessions>().ExecuteAsync(size, CancellationToken.None);
    }

    [Fact]
    public async Task Explicitly_enabled_host_runs_one_real_batch_and_disabled_default_leaves_data_alone()
    {
        await SeedAsync(_now.AddDays(-1));
        await SeedAsync(_now);
        using (var disabled = new RegistrationApp(_database.ConnectionString))
        using (var client = disabled.CreateHttpsClient())
        {
            using var response = await client.GetAsync("/health/live");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            // Default must not even register an active cleanup runner.
            var service = Assert.Single(disabled.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SessionCleanupService>());
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
            await using var context = Context();
            Assert.Equal(2, await context.Set<AuthSession>().CountAsync());
        }
        using var logs = new CleanupLogProbe();
        using var enabled = new RegistrationApp(_database.ConnectionString, new RetentionClock(_now), logs,
            new Dictionary<string, string?> { ["Identity:SessionCleanup:Enabled"] = "true", ["Identity:SessionCleanup:BatchSize"] = "1" });
        using var activeClient = enabled.CreateHttpsClient();
        Assert.Single(enabled.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SessionCleanupService>());
        Assert.Equal(1, await logs.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        await using var remaining = Context();
        Assert.Equal(1, await remaining.Set<AuthSession>().CountAsync());
        Assert.Equal(2, await remaining.Users.CountAsync());
    }

    [Fact]
    public async Task Expired_sessions_and_refreshes_are_removed_without_deleting_accounts()
    {
        await SeedAsync(_now.AddSeconds(-1), consumedRefresh: true);
        await SeedAsync(_now); // Absolute deadline is inclusive.
        var active = await SeedAsync(_now.AddDays(1));
        Assert.Equal(2, await PruneAsync());
        await using var context = Context();
        Assert.Equal(active.Id, (await context.Set<AuthSession>().SingleAsync()).Id);
        Assert.Equal(1, await context.Set<SessionRefreshToken>().CountAsync());
        Assert.Equal(3, await context.Users.CountAsync());
        Assert.Equal(0, await PruneAsync());
    }

    [Fact]
    public async Task Access_expiry_revocation_and_consumed_refresh_do_not_remove_an_unexpired_family()
    {
        var renewable = await SeedAsync(_now.AddDays(1), consumedRefresh: true);
        var revoked = await SeedAsync(_now.AddDays(1), revoked: true, consumedRefresh: true);
        Assert.True(renewable.AccessExpiresAt < _now);
        Assert.Equal(0, await PruneAsync());
        await using var context = Context();
        Assert.Equal(2, await context.Set<SessionRefreshToken>().CountAsync());
        Assert.Equal(2, await context.Set<AuthSession>().CountAsync());
        Assert.Contains(await context.Set<AuthSession>().Select(session => session.Id).ToListAsync(), id => id == revoked.Id);
    }

    [Fact]
    public async Task Batch_is_bounded_oldest_first_and_can_drain_in_multiple_runs()
    {
        var oldest = await SeedAsync(_now.AddDays(-2));
        await SeedAsync(_now.AddDays(-1));
        await SeedAsync(_now);
        Assert.Equal(1, await PruneAsync(1));
        await using (var context = Context()) Assert.False(await context.Set<AuthSession>().AnyAsync(session => session.Id == oldest.Id));
        Assert.Equal(2, await PruneAsync(2));
        Assert.Equal(0, await PruneAsync(2));
    }

    [Fact]
    public async Task Locked_session_is_skipped_and_can_be_cleaned_after_its_transaction_finishes()
    {
        var locked = await SeedAsync(_now.AddDays(-2));
        var available = await SeedAsync(_now.AddDays(-1));
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT \"Id\" FROM identity.\"Sessions\" WHERE \"Id\" = @id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", locked.Id);
        await command.ExecuteScalarAsync();
        Assert.Equal(1, await PruneAsync(1).WaitAsync(TimeSpan.FromSeconds(10)));
        await using (var context = Context())
        {
            Assert.True(await context.Set<AuthSession>().AnyAsync(session => session.Id == locked.Id));
            Assert.False(await context.Set<AuthSession>().AnyAsync(session => session.Id == available.Id));
        }
        await transaction.RollbackAsync();
        Assert.Equal(1, await PruneAsync(1));
    }

    [Fact]
    public async Task Two_runners_remove_each_expired_session_once_without_exceeding_their_batches()
    {
        for (var i = 0; i < 4; i++) await SeedAsync(_now.AddMinutes(-i));
        var counts = await Task.WhenAll(PruneAsync(2), PruneAsync(2));
        Assert.All(counts, count => Assert.InRange(count, 0, 2));
        Assert.Equal(4, counts.Sum());
        await using var context = Context();
        Assert.Equal(0, await context.Set<AuthSession>().CountAsync());
        Assert.Equal(0, await context.Set<SessionRefreshToken>().CountAsync());
        Assert.Equal(4, await context.Users.CountAsync());
    }
}

public sealed class SessionCleanupPolicyTests
{
    private sealed class StoreProbe : ISessionRetentionStore
    {
        public int Calls;
        public Task<int> DeleteExpiredBatchAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(0);
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Invalid_batch_is_rejected_before_calling_the_store(int size)
    {
        var store = new StoreProbe();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new PruneExpiredSessions(store, TimeProvider.System).ExecuteAsync(size, CancellationToken.None));
        Assert.Equal(0, store.Calls);
    }
    [Fact]
    public async Task Cancellation_is_respected_before_starting_work()
    {
        var store = new StoreProbe();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PruneExpiredSessions(store, TimeProvider.System).ExecuteAsync(100, canceled.Token));
        Assert.Equal(0, store.Calls);
    }
}
