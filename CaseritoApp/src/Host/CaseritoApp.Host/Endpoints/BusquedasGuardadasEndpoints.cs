using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Application.Busquedas;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record CrearBusquedaGuardadaRequest(
    string? PalabraClave,
    string? Categoria,
    string? Ciudad,
    decimal? PrecioMinimo,
    decimal? PrecioMaximo,
    string? Estado);

public static class BusquedasGuardadasEndpoints
{
    public static IEndpointRouteBuilder MapBusquedasGuardadasEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/busquedas-guardadas").RequireAuthorization();

        grupo.MapGet("/", ListarAsync)
            .Produces<IReadOnlyList<BusquedaGuardadaDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/", CrearAsync)
            .RequireRateLimiting("busquedas-crear")
            .Accepts<CrearBusquedaGuardadaRequest>("application/json")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapDelete("/{id:guid}", EliminarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ListarBusquedasGuardadasQuery(userId), ct);
        return Results.Ok(resultado);
    }

    private static async Task<IResult> CrearAsync(
        CrearBusquedaGuardadaRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new CrearBusquedaGuardadaCommand(
                userId,
                request.PalabraClave,
                request.Categoria,
                request.Ciudad,
                request.PrecioMinimo,
                request.PrecioMaximo,
                request.Estado), ct);

            return resultado.EsExito
                ? Results.Created($"/api/busquedas-guardadas/{resultado.Valor}", resultado.Valor)
                : ProblemaDesdeError(resultado.Error);
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

    private static async Task<IResult> EliminarAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new EliminarBusquedaGuardadaCommand(id, userId), ct);

        return resultado.EsExito
            ? Results.NoContent()
            : ProblemaDesdeError(resultado.Error);
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out userId) && userId != Guid.Empty;
    }

    private static IResult ProblemaDesdeError(Error error) => error.Code switch
    {
        "busqueda_guardada_no_encontrada" => Results.Problem(
            title: error.Code,
            detail: "El recurso no existe o no pertenece al usuario.",
            statusCode: StatusCodes.Status404NotFound),
        _ => Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest),
    };
}
