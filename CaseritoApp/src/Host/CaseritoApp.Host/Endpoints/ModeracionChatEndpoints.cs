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
}
