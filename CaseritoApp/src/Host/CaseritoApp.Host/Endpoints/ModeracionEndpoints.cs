using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Moderacion;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record ReportarAvisoRequest(string Motivo, string? Detalle);
public sealed record ReporteCreadoResponse(Guid Id);

public static class ModeracionEndpoints
{
    public static IEndpointRouteBuilder MapModeracionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/avisos/{id:guid}/reportes", ReportarAsync)
            .RequireAuthorization()
            .Accepts<ReportarAvisoRequest>("application/json")
            .Produces<ReporteCreadoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var admin = app.MapGroup("/api/admin/moderacion")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.PublicacionesModerar));
        admin.MapGet("/avisos", ListarAsync).Produces<ResultadoPaginado<AvisoReportadoResumenDto>>();
        admin.MapGet("/avisos/{id:guid}", ObtenerAsync).Produces<AvisoReportadoDto>().ProducesProblem(404);
        admin.MapPost("/avisos/{id:guid}/ocultar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct) =>
            AccionAsync(uid => new OcultarAvisoPorModeracionCommand(id, uid), u, s, ct));
        admin.MapPost("/avisos/{id:guid}/restaurar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct) =>
            AccionAsync(uid => new RestaurarAvisoPorModeracionCommand(id, uid), u, s, ct));
        admin.MapPost("/avisos/{id:guid}/eliminar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct) =>
            AccionAsync(uid => new EliminarAvisoPorModeracionCommand(id, uid), u, s, ct));
        admin.MapPost("/reportes/{id:guid}/descartar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct) =>
            AccionAsync(uid => new DescartarReporteAvisoCommand(id, uid), u, s, ct));
        return app;
    }

    private static async Task<IResult> ReportarAsync(
        Guid id, ReportarAvisoRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var r = await sender.Send(new ReportarAvisoCommand(id, userId, request.Motivo, request.Detalle), ct);
            return r.EsExito
                ? Results.Created($"/api/avisos/{id}/reportes/{r.Valor}", new ReporteCreadoResponse(r.Valor))
                : Error(r.Error);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
        catch (ConflictoConcurrenciaException)
        {
            return Results.Problem(title: "El reporte ya fue registrado.", statusCode: 409);
        }
    }

    private static async Task<IResult> ListarAsync(
        ISender sender, CancellationToken ct, string estado = "Pendiente", int pagina = 1, int tamano = 20)
    {
        try { return Results.Ok(await sender.Send(new ListarAvisosReportadosQuery(estado, pagina, tamano), ct)); }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }

    private static async Task<IResult> ObtenerAsync(
        Guid id, ISender sender, CancellationToken ct, string estado = "Pendiente")
    {
        if (!Enum.TryParse<CaseritoApp.Catalog.Domain.Moderacion.EstadoReporteAviso>(estado, out _))
        {
            return Results.BadRequest();
        }

        var dto = await sender.Send(new ObtenerAvisoReportadoQuery(id, estado), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> AccionAsync(
        Func<Guid, IRequest<Result>> crear, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var r = await sender.Send(crear(userId), ct);
            return r.EsExito ? Results.NoContent() : Error(r.Error);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Results.Problem(title: "La información cambió; vuelve a intentarlo.", statusCode: 409);
        }
    }

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid id) =>
        Guid.TryParse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            usuario.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    private static IResult Error(Error error) => error.Code switch
    {
        ErroresAviso.NoEncontrado or ErroresAviso.ReporteNoEncontrado =>
            Results.Problem(title: error.Code, statusCode: 404),
        ErroresAviso.ReporteDuplicado or ErroresAviso.ReporteYaResuelto or
        ErroresAviso.TransicionModeracionInvalida or ErroresAviso.EliminadoPorModeracion =>
            Results.Problem(title: error.Code, statusCode: 409),
        _ => Results.Problem(title: error.Code, statusCode: 400),
    };
}
