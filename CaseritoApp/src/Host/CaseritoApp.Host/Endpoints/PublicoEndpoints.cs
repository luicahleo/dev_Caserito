using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Identity.Application.Kyc;
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
            .Produces<ResultadoPaginado<AvisoPublicoResumenConVendedorDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .Produces<AvisoPublicoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> BuscarAsync(
        ISender sender,
        IConsultaVerificacionKyc consultaKyc,
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

            // Una sola consulta para toda la página: marcar item a item sería N+1.
            var verificados = await consultaKyc.ObtenerVerificadosAsync(
                resultado.Items.Select(a => a.VendedorId).Distinct().ToList(), ct);

            var items = resultado.Items
                .Select(a => AvisoPublicoResumenConVendedorDto.Desde(a, verificados.Contains(a.VendedorId)))
                .ToList();

            return Results.Ok(new ResultadoPaginado<AvisoPublicoResumenConVendedorDto>(
                items, resultado.Pagina, resultado.Tamano, resultado.Total));
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

/// <summary>
/// Resumen público de un aviso con el sello de identidad de su vendedor. Es un DTO del Host
/// y no de Catalog: ese contexto no puede consultar Identity, así que no debe declarar un
/// campo que no puede calcular.
/// </summary>
public sealed record AvisoPublicoResumenConVendedorDto(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos,
    bool VendedorVerificado)
{
    public static AvisoPublicoResumenConVendedorDto Desde(
        AvisoPublicoResumenDto aviso, bool verificado) =>
        new(aviso.Id, aviso.VendedorId, aviso.Titulo, aviso.Monto, aviso.Moneda,
            aviso.NombreCategoria, aviso.NombreCiudad, aviso.Condicion, aviso.FechaCreacion,
            aviso.Fotos, verificado);
}
