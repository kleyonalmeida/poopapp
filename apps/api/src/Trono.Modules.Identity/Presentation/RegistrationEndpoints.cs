using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Identity.Application;

namespace Trono.Modules.Identity.Presentation;

public sealed record RegisterRequest(string? Email, string? Password);
public sealed record RegisterResponse(Guid Id);

public static class RegistrationEndpoints
{
    public static void MapRegistration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/register", async (
            RegisterRequest request, RegisterAccount registerAccount, CancellationToken cancellationToken) =>
        {
            var result = await registerAccount.ExecuteAsync(
                new RegisterAccountCommand(request.Email, request.Password), cancellationToken);

            return result.Status switch
            {
                RegistrationStatus.Created => Results.Json(new RegisterResponse(result.Id!.Value), statusCode: 201),
                RegistrationStatus.Invalid => Results.ValidationProblem(result.Errors!),
                RegistrationStatus.Duplicate => Results.Problem(statusCode: 409, title: "Não foi possível criar a conta."),
                _ => throw new InvalidOperationException("Resultado de cadastro desconhecido.")
            };
        })
        .RequireRateLimiting(IdentityModule.RegistrationRateLimitPolicy)
        .WithName("RegisterAccount")
        .WithTags("Identity")
        .Produces<RegisterResponse>(201)
        .ProducesValidationProblem()
        .ProducesProblem(409)
        .ProducesProblem(429);
    }
}
