using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Trono.Modules.Identity.Application;
using Trono.Modules.Identity.Infrastructure;
using Trono.Modules.Identity.Presentation;

namespace Trono.Modules.Identity;

public static class IdentityModule
{
    public const string ReadinessTag = "ready";
    public const string RegistrationRateLimitPolicy = "identity-registration";
    public const string LoginRateLimitPolicy = "identity-login";
    public const string RefreshRateLimitPolicy = "identity-refresh";
    public const string AccountDeletionRateLimitPolicy = "identity-account-deletion";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("Identity")
                ?? throw new InvalidOperationException("Configure a conexão do módulo Identity."),
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        services.AddHealthChecks().AddCheck<IdentityReadinessCheck>("identity", tags: [ReadinessTag],
            timeout: TimeSpan.FromSeconds(3));

        services.AddIdentityCore<Account>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<IdentityDbContext>();

        services.AddScoped<IAccountRegistrationStore, AccountRegistrationStore>();
        services.AddScoped<RegisterAccount>();
        services.AddSingleton<RegisterAccountValidator>();
        services.AddScoped<IAccountDeletionStore, AccountDeletionStore>();
        services.AddScoped<DeleteOwnAccount>();
        services.AddSingleton<AccountDeletionValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DummyPasswordHash>();
        services.AddScoped<IAccountSessions, AccountSessions>();
        services.AddScoped<IAccountProfileStore, AccountProfileStore>();
        services.AddScoped<UpdateOwnProfile>();
        services.AddScoped<IAccountSettingsStore, AccountSettingsStore>();
        services.AddScoped<UpdateOwnSettings>();
        services.AddSingleton<NicknameValidator>();
        services.AddScoped<LoginAccount>();
        services.AddScoped<RefreshSession>();
        services.AddScoped<ISessionRetentionStore, SessionRetentionStore>();
        services.AddScoped<PruneExpiredSessions>();
        services.AddOptions<SessionCleanupOptions>().Bind(configuration.GetSection("Identity:SessionCleanup"))
            .Validate(options => options.BatchSize is >= 1 and <= 1000, "SessionCleanup:BatchSize deve estar entre 1 e 1000.")
            .Validate(options => options.IntervalMinutes is >= 1 and <= 1440, "SessionCleanup:IntervalMinutes deve estar entre 1 e 1440.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Identity")),
                "SessionCleanup habilitado exige conexão Identity configurada.")
            .ValidateOnStart();
        services.AddHostedService<SessionCleanupService>();
        services.AddSingleton<LoginAccountValidator>();
        services.AddSingleton<RefreshSessionValidator>();
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AccountDeletionRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.AddPolicy(RefreshRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.AddPolicy(RegistrationRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, _) =>
                await Results.Problem(statusCode: 429, title: "Muitas tentativas. Aguarde e tente novamente.")
                    .ExecuteAsync(context.HttpContext);
        });

        return services;
    }

    public static IApplicationBuilder UseIdentityTransport(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/auth") || context.Request.Path.StartsWithSegments("/api/v1/me"))
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
}
