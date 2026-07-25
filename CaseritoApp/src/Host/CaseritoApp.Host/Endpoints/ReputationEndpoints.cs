using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;
using CaseritoApp.Reputation.Infrastructure;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record CrearResenaRequest(int Puntuacion, string Comentario);

public static class ReputationEndpoints
{
    public static IEndpointRouteBuilder MapReputationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/reputacion").RequireAuthorization();

        grupo.MapGet("/ordenes/{orderId:guid}", ObtenerEstadoAsync)
            .RequireRateLimiting("reputation-consultas")
            .Produces<EstadoResenaOrdenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/ordenes/{orderId:guid}/resenas", CrearAsync)
            .RequireRateLimiting("reputation-crear")
            .Accepts<CrearResenaRequest>("application/json")
            .Produces<ResenaCreadaDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> ObtenerEstadoAsync(
        Guid orderId,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(
            new ObtenerEstadoResenaOrdenQuery(orderId, actorId),
            ct);
        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : DesdeError(resultado.Error);
    }

    private static async Task<IResult> CrearAsync(
        Guid orderId,
        CrearResenaRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new CrearResenaCommand(
                orderId,
                actorId,
                request.Puntuacion,
                request.Comentario), ct);
            return resultado.EsExito
                ? Results.Created(
                    $"/api/reputacion/ordenes/{orderId}",
                    resultado.Valor)
                : DesdeError(resultado.Error);
        }
        catch (ConflictoUnicidadReputationException)
        {
            return DesdeError(new Error(
                ErroresResena.Duplicada,
                "La reseña ya fue enviada."));
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.Select(error => error.ErrorMessage).ToArray()));
        }
    }

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId) && userId != Guid.Empty;
    }

    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresResena.OrdenNoDisponible => Results.Problem(
            title: error.Code,
            detail: "El recurso no está disponible.",
            statusCode: StatusCodes.Status404NotFound),
        ErroresResena.Duplicada => Results.Problem(
            title: error.Code,
            detail: "No se pudo completar la operación.",
            statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(
            title: error.Code,
            detail: "La solicitud no es válida.",
            statusCode: StatusCodes.Status400BadRequest),
    };
}
