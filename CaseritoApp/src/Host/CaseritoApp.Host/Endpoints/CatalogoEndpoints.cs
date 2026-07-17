using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Catalogo;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints de catálogos de referencia bajo <c>/api/catalogo</c>.</summary>
public static class CatalogoEndpoints
{
    /// <summary>Mapea los endpoints de referencia (todos <c>[Authorize]</c>).</summary>
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/catalogo").RequireAuthorization();

        grupo.MapGet("/categorias", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCategoriasQuery(), ct)))
            .Produces<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/ciudades", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCiudadesQuery(), ct)))
            .Produces<IReadOnlyList<CiudadDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
