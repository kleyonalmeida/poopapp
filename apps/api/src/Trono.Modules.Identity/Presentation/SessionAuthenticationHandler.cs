using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";
    public const string SessionIdClaim = "session_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values)) return AuthenticateResult.NoResult();
        if (values.Count != 1 || !AuthenticationHeaderValue.TryParse(values[0], out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(header.Parameter))
            return AuthenticateResult.Fail("Credencial inválida.");

        // Resolve lazily so a public healthcheck without credentials never needs a database connection.
        var sessions = Context.RequestServices.GetRequiredService<IAccountSessions>();
        var identity = await sessions.AuthenticateAsync(header.Parameter, Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Sessão inválida.");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, identity.AccountId.ToString()),
            new Claim(SessionIdClaim, identity.SessionId.ToString())
        ], SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await Results.Problem(statusCode: 401, title: "Autenticação necessária.").ExecuteAsync(Context);
    }
}
