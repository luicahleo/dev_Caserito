using CaseritoApp.Catalog.Application.Avisos;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints anónimos de descubrimiento de avisos bajo <c>/api/publico/avisos</c>.</summary>
public static class PublicoEndpoints
{
    /// <summary>Mapea los endpoints públicos de descubrimiento (anónimos, solo avisos Activos).</summary>
    public static IEndpointRouteBuilder MapPublicoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/publico/avisos").AllowAnonymous();

        grupo.MapGet("/", BuscarAsync)
            .Produces<ResultadoPaginado<AvisoPublicoResumenDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .Produces<AvisoPublicoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> BuscarAsync(
        ISender sender,
        CancellationToken ct,
        string? q = null,
        Guid? categoriaId = null,
        Guid? ciudadId = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        string? condicion = null,
        int pagina = 1,
        int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(
                new BuscarAvisosQuery(q, categoriaId, ciudadId, precioMin, precioMax, condicion, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ISender sender, CancellationToken ct)
    {
        var dto = await sender.Send(new ObtenerAvisoPublicoQuery(id), ct);
        return dto is not null ? Results.Ok(dto) : Results.NotFound();
    }
}
