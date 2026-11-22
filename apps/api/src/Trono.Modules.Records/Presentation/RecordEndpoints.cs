using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trono.Modules.Records.Application;
using Trono.Modules.Records.Domain;

namespace Trono.Modules.Records.Presentation;

public sealed record RecordRequest([property: JsonRequired] string? OccurredAt,
    [property: JsonRequired] int? DurationSeconds, [property: JsonRequired] string? Type, [property: JsonRequired] string? Notes);

public static class RecordEndpoints
{
    public static void MapOwnRecords(this IEndpointRouteBuilder endpoints)
    {
        var records = endpoints.MapGroup("/api/v1/records").RequireAuthorization().WithTags("Records");
        records.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (RecordInputException error) { return Results.ValidationProblem(error.Errors); }
        });
        records.MapGet("", async (ClaimsPrincipal user, OwnRecords useCase, CancellationToken token, int offset = 0, int limit = 20) =>
            Results.Ok(await useCase.ListAsync(Owner(user), offset, limit, token)));
        records.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, OwnRecords useCase, CancellationToken token) =>
        {
            var record = await useCase.FindAsync(Owner(user), id, token);
            return record is null ? Missing() : Results.Ok(record);
        });
        records.MapPost("", async (RecordRequest request, ClaimsPrincipal user, HttpContext context, OwnRecords useCase, CancellationToken token) =>
        {
            if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var key)) key = Guid.Empty;
            var result = await useCase.CreateAsync(Owner(user), key, request.OccurredAt, request.DurationSeconds, request.Type, request.Notes, token);
            return result.Status switch
            {
                CreationStatus.Created => Results.Created($"/api/v1/records/{result.Record!.Id}", result.Record),
                CreationStatus.Replayed => Results.Ok(result.Record),
                CreationStatus.Conflict => Results.Problem(statusCode: 409, title: "Esta chave já foi usada para outro envio."),
                CreationStatus.Gone => Results.Problem(statusCode: 410, title: "Este registro já foi excluído."),
                _ => Results.Problem(statusCode: 401, title: "Credenciais inválidas.")
            };
        });
        records.MapPatch("/{id:guid}", async (Guid id, RecordRequest request, ClaimsPrincipal user, OwnRecords useCase, CancellationToken token) =>
        {
            var record = await useCase.UpdateAsync(Owner(user), id, request.OccurredAt, request.DurationSeconds, request.Type, request.Notes, token);
            return record is null ? Missing() : Results.Ok(record);
        });
        records.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, OwnRecords useCase, CancellationToken token) =>
            await useCase.DeleteAsync(Owner(user), id, token) ? Results.NoContent() : Missing());
    }
    private static Guid Owner(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Missing() => Results.Problem(statusCode: 404, title: "Registro indisponível.");
}
