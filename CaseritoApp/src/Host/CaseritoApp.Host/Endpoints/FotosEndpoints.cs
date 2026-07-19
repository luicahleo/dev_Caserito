using CaseritoApp.Catalog.Application.Fotos;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoint anónimo para servir blobs de fotos de avisos.</summary>
public static class FotosEndpoints
{
    /// <summary>Mapea <c>GET /api/fotos/{clave}</c> (anónimo).</summary>
    public static IEndpointRouteBuilder MapFotosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fotos/{clave}", ObtenerAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> ObtenerAsync(
        string clave, IAlmacenFotosAviso almacen, CancellationToken ct)
    {
        try
        {
            var (contenido, contentType) = await almacen.ObtenerAsync(clave, ct);
            return Results.File(contenido, contentType);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
