using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed record UpdateOwnProfileRequest([property: JsonRequired] string? Nickname);
public sealed record OwnProfileResponse(string? Nickname);

public static class OwnProfileEndpoints
{
    public static void MapOwnProfile(this IEndpointRouteBuilder endpoints)
    {
        var profile = endpoints.MapGroup("/api/v1/me/profile").RequireAuthorization().WithTags("Identity");

        profile.MapGet("", async (ClaimsPrincipal user, IAccountSessions sessions, CancellationToken cancellationToken) =>
        {
            var account = await sessions.GetAccountAsync(SessionEndpoints.CurrentSession(user).AccountId, cancellationToken);
            return account is null ? AccountUnavailable() : Results.Ok(new OwnProfileResponse(account.Nickname));
        }).WithName("GetOwnProfile");

        profile.MapPatch("", async (ClaimsPrincipal user, UpdateOwnProfileRequest request, UpdateOwnProfile update,
            CancellationToken cancellationToken) =>
        {
            var result = await update.ExecuteAsync(SessionEndpoints.CurrentSession(user).AccountId, request.Nickname, cancellationToken);
            if (result.Errors is not null) return Results.ValidationProblem(result.Errors);

            return result.Profile is null ? AccountUnavailable() : Results.Ok(new OwnProfileResponse(result.Profile.Nickname));
        }).WithName("UpdateOwnProfile");
    }

    private static IResult AccountUnavailable() => Results.Problem(statusCode: 401, title: "Credenciais inválidas.");
}
