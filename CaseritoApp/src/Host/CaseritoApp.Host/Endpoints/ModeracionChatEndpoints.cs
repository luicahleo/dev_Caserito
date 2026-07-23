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
            .Produces<IReadOnlyList<ReporteChatColaDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        grupo.MapPost("/reportes/{id:guid}/tomar", TomarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/reportes/{id:guid}/liberar", LiberarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapGet("/reportes/{id:guid}/evidencia", ObtenerEvidenciaAsync)
            .Produces<EvidenciaReporteChatDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
