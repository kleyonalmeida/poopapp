using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trono.Modules.Identity.Application;
using Trono.Modules.Identity.Infrastructure;
using Xunit;

namespace Trono.IntegrationTests;

internal sealed class CleanupLogProbe : ILoggerProvider
{
    public TaskCompletionSource<int> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Sink(this);
    public void Dispose() { }
    private sealed class Sink(CleanupLogProbe owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            owner.Messages.Enqueue(formatter(state, exception));
            if (eventId.Id == 6201 && state is IEnumerable<KeyValuePair<string, object?>> values)
                owner.Completed.TrySetResult((int)values.Single(value => value.Key == "Count").Value!);
            if (eventId.Id == 6202) owner.Failed.TrySetResult();
        }
    }
}

public sealed class SessionCleanupServiceTests
{
    private sealed class ProbeStore : ISessionRetentionStore
    {
        public int Calls;
        public bool Throw;
        public bool WaitForCancellation;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<int> DeleteExpiredBatchAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            if (Throw) throw new InvalidOperationException("private-fixture-details");
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private static ServiceProvider Provider(ProbeStore store, CleanupLogProbe logs) => new ServiceCollection()
        .AddSingleton<ISessionRetentionStore>(store).AddSingleton(TimeProvider.System).AddScoped<PruneExpiredSessions>()
        .AddLogging(logging => logging.AddProvider(logs)).BuildServiceProvider(validateScopes: true);

    [Fact]
    public async Task Disabled_service_finishes_without_resolving_or_calling_the_store()
    {
        var store = new ProbeStore();
        using var logs = new CleanupLogProbe();
        await using var provider = Provider(store, logs);
        using var worker = new SessionCleanupService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new SessionCleanupOptions()),
            TimeProvider.System, provider.GetRequiredService<ILogger<SessionCleanupService>>());
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Failure_is_reported_without_private_details_and_does_not_end_the_worker_or_retry_in_a_loop()
    {
        var store = new ProbeStore { Throw = true };
        using var logs = new CleanupLogProbe();
        await using var provider = Provider(store, logs);
        using var worker = new SessionCleanupService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SessionCleanupOptions { Enabled = true, IntervalMinutes = 1 }), TimeProvider.System,
            provider.GetRequiredService<ILogger<SessionCleanupService>>());
        await worker.StartAsync(CancellationToken.None);
        await Task.WhenAny(logs.Failed.Task, worker.ExecuteTask!).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(logs.Failed.Task.IsCompletedSuccessfully);
        Assert.False(worker.ExecuteTask!.IsCompleted);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-fixture-details", StringComparison.Ordinal));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, store.Calls);
    }

    [Fact]
    public async Task Shutdown_cancels_in_flight_work_without_logging_a_failure()
    {
        var store = new ProbeStore { WaitForCancellation = true };
        using var logs = new CleanupLogProbe();
        await using var provider = Provider(store, logs);
        using var worker = new SessionCleanupService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SessionCleanupOptions { Enabled = true }), TimeProvider.System,
            provider.GetRequiredService<ILogger<SessionCleanupService>>());
        await worker.StartAsync(CancellationToken.None);
        await Task.WhenAny(store.Started.Task, worker.ExecuteTask!).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(store.Started.Task.IsCompletedSuccessfully);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(logs.Failed.Task.IsCompleted);
        Assert.Equal(1, store.Calls);
    }

    [Theory]
    [InlineData("Identity:SessionCleanup:BatchSize", "0")]
    [InlineData("Identity:SessionCleanup:BatchSize", "1001")]
    [InlineData("Identity:SessionCleanup:IntervalMinutes", "0")]
    [InlineData("Identity:SessionCleanup:IntervalMinutes", "1441")]
    public void Invalid_configuration_is_rejected_on_startup(string key, string value)
    {
        using var app = new RegistrationApp("Host=unused;Database=unused;Username=unused", settings:
            new Dictionary<string, string?> { [key] = value });
        Assert.Throws<OptionsValidationException>(() => app.CreateHttpsClient());
    }
    [Fact]
    public void Enabled_cleanup_requires_a_connection_before_starting()
    {
        using var app = new RegistrationApp("", settings: new Dictionary<string, string?> { ["Identity:SessionCleanup:Enabled"] = "true" });
        Assert.Throws<OptionsValidationException>(() => app.CreateHttpsClient());
    }
}
