using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken, DateTimeOffset RefreshExpiresAt)
{
    public static TokenResponse From(SessionTokens value) => new("Bearer", value.AccessToken, value.ExpiresIn, value.RefreshToken, value.RefreshExpiresAt);
}

public static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/login", async (LoginRequest request, LoginAccount login, CancellationToken cancellationToken) =>
        {
            var result = await login.ExecuteAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
            return ToResult(result);
        }).RequireRateLimiting(IdentityModule.LoginRateLimitPolicy).WithName("Login").WithTags("Identity");

        endpoints.MapPost("/api/v1/auth/refresh", async (RefreshRequest request, RefreshSession refresh, CancellationToken cancellationToken) =>
        {
            return ToResult(await refresh.ExecuteAsync(request.RefreshToken, cancellationToken));
        }).RequireRateLimiting(IdentityModule.RefreshRateLimitPolicy).WithName("RefreshSession").WithTags("Identity");

        endpoints.MapPost("/api/v1/auth/logout", async (ClaimsPrincipal user, IAccountSessions sessions, CancellationToken cancellationToken) =>
        {
            await sessions.RevokeAsync(CurrentSession(user), cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().WithName("Logout").WithTags("Identity");

        endpoints.MapGet("/api/v1/me", async (ClaimsPrincipal user, IAccountSessions sessions, CancellationToken cancellationToken) =>
        {
            var account = await sessions.GetAccountAsync(CurrentSession(user).AccountId, cancellationToken);
            return account is not null ? Results.Ok(account) : InvalidCredentials();
        }).RequireAuthorization().WithName("GetOwnAccount").WithTags("Identity");
    }

    private static IResult InvalidCredentials() => Results.Problem(statusCode: 401, title: "Credenciais inválidas.");
    private static IResult ToResult(SessionResult result) => result.Errors is not null
        ? Results.ValidationProblem(result.Errors)
        : result.Tokens is not null ? Results.Ok(TokenResponse.From(result.Tokens)) : InvalidCredentials();
    internal static SessionIdentity CurrentSession(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Guid.Parse(user.FindFirstValue(SessionAuthenticationHandler.SessionIdClaim)!));
}
