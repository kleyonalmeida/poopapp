using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Trono.Modules.Identity;
using Trono.Modules.Identity.Presentation;

var builder = WebApplication.CreateBuilder(args);
// Hosting request summaries include raw query strings, even for rejected credentials.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddProblemDetails();
// Handle malformed request bodies as client errors in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddIdentityModule(builder.Configuration);
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseIdentityTransport();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Probes are independent of request authentication, even when a caller includes a Bearer header.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).ShortCircuit();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(IdentityModule.ReadinessTag)
}).ShortCircuit();
app.MapRegistration();
app.MapSessions();
app.MapOwnProfile();
app.MapOwnSettings();
app.MapAccountDeletion();

app.Run();

public partial class Program;
