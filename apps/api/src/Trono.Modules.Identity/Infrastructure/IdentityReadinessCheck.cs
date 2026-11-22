using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Trono.Modules.Identity.Infrastructure;

internal sealed class IdentityReadinessCheck(IServiceProvider services, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Identity")))
            return HealthCheckResult.Unhealthy();

        try
        {
            var database = services.GetRequiredService<IdentityDbContext>();
            var pending = await database.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any() ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is NpgsqlException or SocketException
            or ArgumentException or InvalidOperationException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
