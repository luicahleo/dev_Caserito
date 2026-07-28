using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Application.Notificaciones;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/notificaciones").RequireAuthorization();

        grupo.MapGet("/", ListarAsync)
            .RequireRateLimiting("notifications-consultas")
            .Produces<PaginaNotificacionesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/no-leidas", ContarNoLeidasAsync)
            .RequireRateLimiting("notifications-conteo")
            .Produces<int>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPatch("/{id:guid}/leida", MarcarLeidaAsync)
            .RequireRateLimiting("notifications-acciones")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPatch("/marcar-todas-leidas", MarcarTodasLeidasAsync)
            .RequireRateLimiting("notifications-acciones")
            .Produces<int>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ISender sender,
        ClaimsPrincipal usuario,
        bool soloNoLeidas = false,
        int pagina = 1,
        int tamano = 20,
        CancellationToken ct = default)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(
            new ListarNotificacionesQuery(userId, soloNoLeidas, pagina, tamano),
            ct);

        return Results.Ok(resultado);
    }

    private static async Task<IResult> ContarNoLeidasAsync(
        ISender sender,
        ClaimsPrincipal usuario,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var total = await sender.Send(new ContarNoLeidasQuery(userId), ct);
        return Results.Ok(total);
    }

    private static async Task<IResult> MarcarLeidaAsync(
        Guid id,
        ISender sender,
        ClaimsPrincipal usuario,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new MarcarLeidaCommand(id, userId), ct);

        return resultado.EsExito
            ? Results.Ok()
            : Results.Problem(
                title: "notificacion_no_disponible",
                detail: "La notificación no está disponible.",
                statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> MarcarTodasLeidasAsync(
        ISender sender,
        ClaimsPrincipal usuario,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new MarcarTodasLeidasCommand(userId), ct);
        return Results.Ok(resultado.Valor);
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out userId) && userId != Guid.Empty;
    }
}
