using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Reputation.Application.Resenas;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record PerfilPublicoConReputacionDto(
    Guid Id,
    string NombreVisible,
    Guid CiudadId,
    string NombreCiudad,
    bool Verificado,
    decimal? Promedio,
    int TotalResenas);

/// <summary>Compone datos públicos de Identity y Reputation sin acoplar sus contextos.</summary>
public static class PerfilesPublicosEndpoints
{
    public static IEndpointRouteBuilder MapPerfilesPublicosEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/publico/usuarios")
            .AllowAnonymous()
            .RequireRateLimiting("reputation-publico");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .Produces<PerfilPublicoConReputacionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/{id:guid}/resenas", ListarResenasAsync)
            .Produces<ResultadoPaginadoResenasDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> ObtenerAsync(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        var perfil = await sender.Send(new ObtenerPerfilPublicoQuery(id), ct);
        if (!perfil.EsExito)
        {
            return NoDisponible(perfil.Error.Code);
        }

        var resumen = await sender.Send(new ObtenerResumenReputacionQuery(id), ct);
        return Results.Ok(new PerfilPublicoConReputacionDto(
            perfil.Valor.Id,
            perfil.Valor.NombreVisible,
            perfil.Valor.CiudadId,
            perfil.Valor.NombreCiudad,
            perfil.Valor.Verificado,
            resumen.Promedio,
            resumen.Total));
    }

    private static async Task<IResult> ListarResenasAsync(
        Guid id,
        ISender sender,
        CancellationToken ct,
        int pagina = 1,
        int tamano = 10)
    {
        var perfil = await sender.Send(new ObtenerPerfilPublicoQuery(id), ct);
        if (!perfil.EsExito)
        {
            return NoDisponible(perfil.Error.Code);
        }

        try
        {
            return Results.Ok(await sender.Send(
                new ListarResenasPublicasQuery(id, pagina, tamano),
                ct));
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

    private static IResult NoDisponible(string codigo) => Results.Problem(
        title: codigo,
        detail: "El recurso no está disponible.",
        statusCode: StatusCodes.Status404NotFound);
}
