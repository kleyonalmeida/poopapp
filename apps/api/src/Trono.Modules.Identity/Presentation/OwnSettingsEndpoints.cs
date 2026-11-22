using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed record UpdateOwnSettingsRequest([property: JsonRequired] bool PushNotificationsEnabled);
public sealed record OwnSettingsResponse(bool PushNotificationsEnabled);

public static class OwnSettingsEndpoints
{
    public static void MapOwnSettings(this IEndpointRouteBuilder endpoints)
    {
        var settings = endpoints.MapGroup("/api/v1/me/settings").RequireAuthorization().WithTags("Identity");

        settings.MapGet("", async (ClaimsPrincipal user, IAccountSessions sessions, CancellationToken cancellationToken) =>
        {
            var account = await sessions.GetAccountAsync(SessionEndpoints.CurrentSession(user).AccountId, cancellationToken);
            return account is null ? AccountUnavailable() : Results.Ok(new OwnSettingsResponse(account.PushNotificationsEnabled));
        }).WithName("GetOwnSettings");

        settings.MapPatch("", async (ClaimsPrincipal user, UpdateOwnSettingsRequest request, UpdateOwnSettings update,
            CancellationToken cancellationToken) =>
        {
            var accountId = SessionEndpoints.CurrentSession(user).AccountId;
            var updated = await update.ExecuteAsync(accountId, request.PushNotificationsEnabled, cancellationToken);
            return updated ? Results.Ok(new OwnSettingsResponse(request.PushNotificationsEnabled)) : AccountUnavailable();
        }).WithName("UpdateOwnSettings");
    }

    private static IResult AccountUnavailable() => Results.Problem(statusCode: 401, title: "Credenciais inválidas.");
}
