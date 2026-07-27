using CaseritoApp.Notifications.Application.PuntosEncuentro;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints anónimos de puntos de encuentro seguros bajo <c>/api/publico/puntos-encuentro</c>.</summary>
public static class PuntosEncuentroEndpoints
{
    /// <summary>Mapea el endpoint público de puntos de encuentro seguros (anónimo, solo activos).</summary>
    public static IEndpointRouteBuilder MapPuntosEncuentroEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/publico/puntos-encuentro").AllowAnonymous();

        grupo.MapGet("/", ListarAsync)
            .Produces<IReadOnlyList<PuntoEncuentroSeguroDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ISender sender,
        CancellationToken ct,
        string? ciudad = null)
    {
        if (string.IsNullOrWhiteSpace(ciudad))
        {
            return Results.BadRequest(new { error = "La ciudad es requerida." });
        }

        var resultado = await sender.Send(new ListarPuntosEncuentroQuery(ciudad), ct);
        return Results.Ok(resultado);
    }
}
