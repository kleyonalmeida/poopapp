using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed record DeleteAccountRequest(string? Password);

public static class AccountDeletionEndpoints
{
    public static void MapAccountDeletion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/v1/me/account", async (
            [FromBody] DeleteAccountRequest request, ClaimsPrincipal user, DeleteOwnAccount delete, CancellationToken cancellationToken) =>
        {
            var result = await delete.ExecuteAsync(SessionEndpoints.CurrentSession(user).AccountId, request.Password, cancellationToken);
            return result.Status switch
            {
                AccountDeletionStatus.Deleted => Results.NoContent(),
                AccountDeletionStatus.Invalid => Results.ValidationProblem(result.Errors!),
                AccountDeletionStatus.Denied => Results.Problem(statusCode: 403, title: "Não foi possível confirmar a exclusão."),
                AccountDeletionStatus.SessionGone => Results.Problem(statusCode: 401, title: "Autenticação necessária."),
                AccountDeletionStatus.Conflict => Results.Problem(statusCode: 409, title: "Não foi possível concluir a exclusão."),
                _ => throw new InvalidOperationException("Resultado de exclusão desconhecido.")
            };
        }).RequireAuthorization().RequireRateLimiting(IdentityModule.AccountDeletionRateLimitPolicy)
            .WithName("DeleteOwnAccount").WithTags("Identity").Produces(204).ProducesValidationProblem()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(429);
    }
}
