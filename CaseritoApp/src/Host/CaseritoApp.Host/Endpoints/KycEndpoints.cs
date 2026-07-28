using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Auth;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo para rechazar una solicitud KYC.</summary>
public sealed record RechazarKycRequest(string Motivo);

/// <summary>Endpoints de KYC: subida y estado del usuario (<c>/api/kyc</c>) y gestión admin (<c>/api/admin/kyc</c>).</summary>
public static class KycEndpoints
{
    /// <summary>Mapea los grupos de KYC de usuario y de administrador.</summary>
    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder app)
    {
        var usuario = app.MapGroup("/api/kyc").RequireAuthorization();
        usuario.MapPost("/", EnviarAsync).DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesValidationProblem();
        usuario.MapGet("/estado", EstadoAsync)
            .Produces<EstadoKycDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        var admin = app.MapGroup("/api/admin/kyc")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.KycRevisar));
        admin.MapGet("/", ListarAsync)
            .Produces<ResultadoPaginado<SolicitudKycResumenDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
        admin.MapGet("/{solicitudId:guid}/documento", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Documento, u, s, ct))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapGet("/{solicitudId:guid}/selfie", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Selfie, u, s, ct))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapPost("/{solicitudId:guid}/aprobar", AprobarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPost("/{solicitudId:guid}/rechazar", RechazarAsync)
            .Accepts<RechazarKycRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> EnviarAsync(
        IFormFile documento, IFormFile selfie, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var docBytes = await LeerAsync(documento, ct);
        var selfieBytes = await LeerAsync(selfie, ct);

        try
        {
            var resultado = await sender.Send(
                new EnviarSolicitudKycCommand(
                    userId, docBytes, documento.ContentType, selfieBytes, selfie.ContentType), ct);
            return DesdeResult(resultado);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Conflicto409();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> EstadoAsync(ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerEstadoKycQuery(userId), ct);
        return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeResult(resultado);
    }

    private static async Task<IResult> ListarAsync(
        ISender sender, CancellationToken ct, string? estado = null, int pagina = 1, int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(new ListarSolicitudesKycQuery(estado, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> BlobAsync(
        Guid solicitudId, TipoBlobKyc tipo, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerBlobKycQuery(solicitudId, adminId, tipo), ct);
        return resultado.EsExito
            ? Results.File(resultado.Valor.Contenido, resultado.Valor.ContentType)
            : DesdeResult(resultado);
    }

    private static async Task<IResult> AprobarAsync(
        Guid solicitudId, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new AprobarSolicitudKycCommand(solicitudId, adminId), ct);
            return DesdeResult(resultado);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Conflicto409();
        }
    }

    private static async Task<IResult> RechazarAsync(
        Guid solicitudId, RechazarKycRequest request, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new RechazarSolicitudKycCommand(solicitudId, adminId, request.Motivo), ct);
            return DesdeResult(resultado);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Conflicto409();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<byte[]> LeerAsync(IFormFile archivo, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    private static IResult Conflicto409() =>
        Results.Problem(
            title: ErroresKyc.ConflictoConcurrencia,
            detail: "La operación entró en conflicto con otra concurrente. Reintente.",
            statusCode: StatusCodes.Status409Conflict);

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    // Mapea el Result de los casos de uso KYC a códigos HTTP.
    private static IResult DesdeResult(Result resultado)
    {
        if (resultado.EsExito)
        {
            return Results.NoContent();
        }

        return resultado.Error.Code switch
        {
            ErroresKyc.SolicitudNoEncontrada =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status404NotFound),
            ErroresKyc.YaVerificado or ErroresKyc.SolicitudPendienteExiste or ErroresKyc.TransicionInvalida =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status409Conflict),
            ErroresKyc.ServicioVerificacionNoDisponible =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status400BadRequest),
        };
    }
}
