using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public static class ModeracionChatEndpoints
{
    public static IEndpointRouteBuilder MapModeracionChatEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/moderacion/chat")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.ChatModerar));

        grupo.MapGet("/reportes", ListarAsync)
            .RequireRateLimiting("chat-consultas")
            .Produces<IReadOnlyList<ReporteChatColaDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/reportes/{id:guid}/tomar", TomarAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/reportes/{id:guid}/liberar", LiberarAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/reportes/{id:guid}/evidencia", ObtenerEvidenciaAsync)
            .RequireRateLimiting("chat-consultas")
            .Produces<EvidenciaReporteChatDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/reportes/{id:guid}/atender", AtenderAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/reportes/{id:guid}/descartar", DescartarAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPut("/reportes/{id:guid}/cierre-conversacion", CerrarConversacionAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapDelete("/reportes/{id:guid}/cierre-conversacion", ReabrirConversacionAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        EstadoReporteChat? estado,
        string? cursor,
        int limite,
        ISender sender,
        CancellationToken ct)
    {
        _ = cursor;
        var reportes = await sender.Send(new ListarReportesChatQuery(estado, limite), ct);
        return Results.Ok(reportes);
    }

    private static Task<IResult> TomarAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct) =>
        EjecutarAsync(usuario, moderadorId => new TomarReporteChatCommand(id, moderadorId), sender, ct);

    private static Task<IResult> LiberarAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct) =>
        EjecutarAsync(usuario, moderadorId => new LiberarReporteChatCommand(id, moderadorId), sender, ct);

    private static Task<IResult> AtenderAsync(
        Guid id,
        AtenderReporteChatRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct) =>
        EjecutarAsync(
            usuario,
            moderadorId => new AtenderReporteChatCommand(
                id, moderadorId, request.CerrarConversacion),
            sender,
            ct);

    private static Task<IResult> DescartarAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct) =>
        EjecutarAsync(
            usuario,
            moderadorId => new DescartarReporteChatCommand(id, moderadorId),
            sender,
            ct);

    private static Task<IResult> CerrarConversacionAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct) =>
        EjecutarAsync(
            usuario,
            moderadorId => new CerrarPorModeracionCommand(id, moderadorId),
            sender,
            ct);

    private static Task<IResult> ReabrirConversacionAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct) =>
        EjecutarAsync(
            usuario,
            moderadorId => new ReabrirPorModeracionCommand(id, moderadorId),
            sender,
            ct);

    private static async Task<IResult> ObtenerEvidenciaAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var moderadorId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(
            new ObtenerEvidenciaReporteChatQuery(id, moderadorId), ct);
        return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeError(resultado.Error);
    }

    private static async Task<IResult> EjecutarAsync(
        ClaimsPrincipal usuario,
        Func<Guid, IRequest<Result>> crearComando,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var moderadorId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(crearComando(moderadorId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid usuarioId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out usuarioId);
    }

    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresModeracionChat.NoEncontrado => Results.Problem(
            title: ErroresModeracionChat.NoEncontrado,
            detail: "El reporte no está disponible.",
            statusCode: StatusCodes.Status404NotFound),
        _ => Results.Problem(
            title: ErroresModeracionChat.TransicionInvalida,
            detail: "No se pudo completar la acción.",
            statusCode: StatusCodes.Status409Conflict),
    };
}

public sealed record AtenderReporteChatRequest(bool CerrarConversacion);
