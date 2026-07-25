using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Host.Orders;
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using CaseritoApp.Orders.Infrastructure;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record SolicitarOrdenRequest(Guid AvisoId);

public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/orders").RequireAuthorization();

        grupo.MapPost("/", SolicitarAsync)
            .RequireRateLimiting("orders-crear")
            .Produces<OrdenCreadaDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/{id:guid}/aceptar", AceptarAsync)
            .RequireRateLimiting("orders-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/{id:guid}/cancelar", CancelarAsync)
            .RequireRateLimiting("orders-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/{id:guid}/marcar-vendido", MarcarVendidoAsync)
            .RequireRateLimiting("orders-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/{id:guid}/confirmar-completado", ConfirmarCompletadoAsync)
            .RequireRateLimiting("orders-acciones")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/", ListarAsync)
            .RequireRateLimiting("orders-consultas")
            .Produces<ResultadoPaginadoOrdenes>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .RequireRateLimiting("orders-consultas")
            .Produces<OrdenDetalleDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> SolicitarAsync(
        SolicitarOrdenRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new SolicitarOrdenCommand(
                request.AvisoId,
                actorId,
                EstaVerificado(usuario)), ct);
            return resultado.EsExito
                ? Results.Created($"/api/orders/{resultado.Valor.Id}", resultado.Valor)
                : DesdeError(resultado.Error);
        }
        catch (Exception ex) when (EsConflictoPersistencia(ex))
        {
            return ConflictoPersistencia();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> AceptarAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new AceptarOrdenCommand(id, actorId), ct);
            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (Exception ex) when (EsConflictoPersistencia(ex))
        {
            return ConflictoPersistencia();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> CancelarAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new CancelarOrdenCommand(id, actorId), ct);
            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (Exception ex) when (EsConflictoPersistencia(ex))
        {
            return ConflictoPersistencia();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ListarAsync(
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct,
        string rol = "comprador",
        string? estado = null,
        int pagina = 1,
        int tamano = 20)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Ok(await sender.Send(
                new ListarOrdenesQuery(actorId, rol, estado, pagina, tamano), ct));
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> MarcarVendidoAsync(
        Guid id,
        ClaimsPrincipal usuario,
        IOrquestadorCierreOrden orquestador,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await orquestador.MarcarVendidaAsync(id, actorId, ct);
            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (CatalogPendienteException)
        {
            return Results.Problem(
                title: "orders.catalog_pendiente",
                detail: "No se pudo completar la operación. Inténtelo nuevamente.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex) when (EsConflictoPersistencia(ex))
        {
            return ConflictoPersistencia();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ConfirmarCompletadoAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new ConfirmarCierreOrdenCommand(id, actorId), ct);
            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (Exception ex) when (EsConflictoPersistencia(ex))
        {
            return ConflictoPersistencia();
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ObtenerAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var actorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new ObtenerOrdenQuery(id, actorId), ct);
            return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId) && userId != Guid.Empty;
    }

    private static bool EstaVerificado(ClaimsPrincipal usuario) =>
        string.Equals(
            usuario.FindFirstValue("verificado"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static bool EsConflictoPersistencia(Exception ex) =>
        ex is ConflictoUnicidadOrdersException or ConflictoConcurrenciaException;

    private static IResult ConflictoPersistencia() => Results.Problem(
        title: "orders.conflicto",
        detail: "No se pudo completar la operación. Inténtelo nuevamente.",
        statusCode: StatusCodes.Status409Conflict);

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(error => error.ErrorMessage).ToArray()));

    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresOrden.NoEncontrada =>
            Results.Problem(
                title: error.Code,
                detail: "El recurso no está disponible.",
                statusCode: StatusCodes.Status404NotFound),
        ErroresOrden.NoVerificado =>
            Results.Problem(
                title: error.Code,
                detail: "Debe completar la verificación de identidad.",
                statusCode: StatusCodes.Status403Forbidden),
        ErroresOrden.Duplicada
            or ErroresOrden.TransicionInvalida
            or ErroresAviso.TransicionInvalida
            or ErroresAviso.VentaIncompatible =>
            Results.Problem(
                title: error.Code,
                detail: "No se pudo completar la operación.",
                statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(
            title: error.Code,
            detail: "La solicitud no es válida.",
            statusCode: StatusCodes.Status400BadRequest),
    };
}
