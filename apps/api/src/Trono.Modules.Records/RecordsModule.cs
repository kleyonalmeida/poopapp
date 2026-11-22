using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trono.Modules.Records.Application;
using Trono.Modules.Records.Infrastructure;

namespace Trono.Modules.Records;

public static class RecordsModule
{
    public static IServiceCollection AddRecordsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RecordsDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Identity") ?? throw new InvalidOperationException("Configure a conexão do banco TRONO."),
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "records")));
        services.AddScoped<IOwnRecordStore, OwnRecordStore>();
        services.AddScoped<OwnRecords>();
        services.AddHealthChecks().AddCheck<RecordsReadinessCheck>("records", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
        return services;
    }
    public static IApplicationBuilder UseRecordsTransport(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/records"))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps)
            {
                await Results.Problem(statusCode: 400, title: "HTTPS obrigatório.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    });
}

internal sealed class RecordsReadinessCheck(IServiceProvider services, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Identity"))) return HealthCheckResult.Unhealthy();
        try
        {
            var pending = await services.GetRequiredService<RecordsDbContext>().Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any() ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is Npgsql.NpgsqlException or System.Net.Sockets.SocketException
            or ArgumentException or InvalidOperationException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
