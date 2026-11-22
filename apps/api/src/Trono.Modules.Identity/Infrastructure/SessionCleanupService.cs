using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Infrastructure;

public sealed class SessionCleanupOptions
{
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 100;
    public int IntervalMinutes { get; set; } = 15;
}

public sealed class SessionCleanupService(
    IServiceScopeFactory scopes, IOptions<SessionCleanupOptions> options, TimeProvider clock,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<PruneExpiredSessions>()
                    .ExecuteAsync(settings.BatchSize, stoppingToken);
                logger.LogInformation(new EventId(6201, "SessionCleanupCompleted"),
                    "Limpeza de sessões concluída: {Count} sessões expiradas removidas.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                // Do not include exceptions, SQL parameters, IDs, token hashes or connection details.
                logger.LogWarning(new EventId(6202, "SessionCleanupFailed"),
                    "A limpeza de sessões não foi concluída; nova tentativa no próximo intervalo.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
