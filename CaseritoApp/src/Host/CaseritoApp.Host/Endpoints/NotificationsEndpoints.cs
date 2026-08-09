using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Host.Chat;
using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Application.Push;
using CaseritoApp.Notifications.Infrastructure.Push;
using MediatR;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Host.Endpoints;

public static class NotificationsEndpoints
{
    public sealed record SuscripcionPushRequest(
        string DispositivoId, string Endpoint, string P256dh, string Auth);
    public sealed record ConfirmarEntregaPushRequest(string Comprobante);
    public sealed record ConfiguracionPushResponse(string ClavePublica);

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

        grupo.MapGet("/push/configuracion", ObtenerConfiguracionPush)
            .Produces<ConfiguracionPushResponse>(StatusCodes.Status200OK);
        grupo.MapPut("/push/suscripcion", RegistrarPushAsync)
            .RequireRateLimiting("notifications-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        grupo.MapDelete("/push/suscripcion/{dispositivoId}", RevocarPushAsync)
            .RequireRateLimiting("notifications-acciones")
            .Produces(StatusCodes.Status204NoContent);
        grupo.MapPost("/push/confirmar-entrega", ConfirmarEntregaPushAsync)
            .AllowAnonymous()
            .RequireRateLimiting("notifications-acciones")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static IResult ObtenerConfiguracionPush(IOptions<OpcionesWebPush> opciones) =>
        Results.Ok(new ConfiguracionPushResponse(
            opciones.Value.Habilitado ? opciones.Value.ClavePublica : string.Empty));

    private static async Task<IResult> RegistrarPushAsync(
        SuscripcionPushRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new RegistrarSuscripcionPushCommand(
            userId, request.DispositivoId, request.Endpoint, request.P256dh, request.Auth), ct);
        return resultado.EsExito
            ? Results.NoContent()
            : Results.Problem(
                title: "suscripcion_no_valida",
                detail: "La suscripción no es válida.",
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> RevocarPushAsync(
        string dispositivoId,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        await sender.Send(new RevocarSuscripcionPushCommand(userId, dispositivoId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmarEntregaPushAsync(
        ConfirmarEntregaPushRequest request,
        IProtectorComprobantesEntrega protector,
        ISender sender,
        IPublicadorEventosGlobalesChat publicador,
        CancellationToken ct)
    {
        if (!protector.TryValidar(request.Comprobante, out var datos))
        {
            return Results.NoContent();
        }

        var resultado = await sender.Send(new MarcarEntregaCommand(
            datos.ConversacionId, datos.DestinatarioId, datos.Secuencia), ct);
        if (resultado.EsExito)
        {
            await publicador.PublicarEstadoAsync(
                resultado.Valor.DestinatarioEstadoId,
                new EstadoMensajesActualizadoDto(
                    datos.ConversacionId,
                    resultado.Valor.UltimaSecuenciaEntregada,
                    resultado.Valor.UltimaSecuenciaLeida),
                ct);
        }

        return Results.NoContent();
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
