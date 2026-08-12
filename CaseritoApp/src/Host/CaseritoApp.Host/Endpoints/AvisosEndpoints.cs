using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo para crear un aviso.</summary>
public sealed record CrearAvisoRequest(
    string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId);

/// <summary>Cuerpo para editar un aviso.</summary>
public sealed record EditarAvisoRequest(
    string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId);

/// <summary>Respuesta de creación de un aviso.</summary>
public sealed record AvisoCreadoResponse(Guid Id);

/// <summary>Respuesta de subida de foto.</summary>
public sealed record FotoCreadaResponse(Guid Id);

/// <summary>Endpoints del dueño sobre sus avisos, bajo <c>/api/avisos</c>.</summary>
public static class AvisosEndpoints
{
    /// <summary>Mapea los endpoints de avisos (todos <c>[Authorize]</c>).</summary>
    public static IEndpointRouteBuilder MapAvisosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/avisos").RequireAuthorization();

        grupo.MapPost("/", CrearAsync)
            .RequireAuthorization(PoliticasAutorizacion.EmailConfirmado)
            .Accepts<CrearAvisoRequest>("application/json")
            .Produces<AvisoCreadoResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequireAuthorization(PoliticasAutorizacion.EmailConfirmado)
            .Accepts<EditarAvisoRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        grupo.MapPost("/{id:guid}/pausar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new PausarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/reactivar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new ReactivarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new EliminarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/mios", ListarMiosAsync)
            .Produces<ResultadoPaginado<AvisoResumenDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        grupo.MapGet("/mios/{id:guid}", ObtenerMioAsync)
            .Produces<AvisoDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/fotos", SubirFotoAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .WithMetadata(new RequestFormLimitsAttribute
            {
                MultipartBodyLengthLimit = 26 * 1024 * 1024,
            })
            .DisableAntiforgery()
            .Produces<FotoCreadaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}/fotos/{fotoId:guid}", BorrarFotoAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CrearAsync(
        CrearAvisoRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new CrearAvisoCommand(
                    userId, TieneIdentidadHabilitada(usuario), request.Titulo, request.Descripcion,
                    request.Monto, request.Condicion, request.CategoriaId, request.CiudadId), ct);

            return resultado.EsExito
                ? Results.Created($"/api/avisos/mios/{resultado.Valor}", new AvisoCreadoResponse(resultado.Valor))
                : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> EditarAsync(
        Guid id, EditarAvisoRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new EditarAvisoCommand(
                    id, userId, request.Titulo, request.Descripcion,
                    request.Monto, request.Condicion, request.CategoriaId, request.CiudadId), ct);

            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> TransicionAsync(
        Func<Guid, IRequest<Result>> fabricaComando, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(fabricaComando(userId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }

    private static async Task<IResult> ListarMiosAsync(
        ClaimsPrincipal usuario, ISender sender, CancellationToken ct, int pagina = 1, int tamano = 20)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new ListarMisAvisosQuery(userId, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ObtenerMioAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerMiAvisoQuery(id, userId), ct);
        return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeError(resultado.Error);
    }

    private static async Task<IResult> SubirFotoAsync(
        Guid id, IFormFile? file, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Results.Problem(
                title: "archivo_requerido",
                detail: "Se debe adjuntar un archivo de imagen.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > ValidacionFotoAviso.LimiteBytes)
        {
            return Results.Problem(
                title: "imagen_invalida",
                detail: "No se pudo procesar la imagen.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        byte[] contenido;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            contenido = ms.ToArray();
        }

        try
        {
            var resultado = await sender.Send(
                new SubirFotoAvisoCommand(id, userId, contenido, file.ContentType ?? string.Empty), ct);

            return resultado.EsExito
                ? Results.Created($"/api/avisos/mios/{id}", new FotoCreadaResponse(resultado.Valor))
                : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> BorrarFotoAsync(
        Guid id, Guid fotoId, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new BorrarFotoAvisoCommand(id, fotoId, userId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    private static bool TieneIdentidadHabilitada(ClaimsPrincipal usuario) =>
        string.Equals(
            usuario.FindFirstValue(ClaimsApp.IdentidadHabilitada),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    // Mapea el Error de dominio de Catalog a códigos HTTP.
    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresAviso.NoEncontrado =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        ErroresAviso.NoEsPropietario or ErroresAviso.NoVerificado =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status403Forbidden),
        ErroresAviso.TransicionInvalida or ErroresAviso.EliminadoPorModeracion =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        _ =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
