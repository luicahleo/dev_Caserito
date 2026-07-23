using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Application.Paginacion;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Chat;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

public sealed record IniciarConversacionRequest(Guid AvisoId);

public sealed record EnviarMensajeRequest(Guid ClaveIdempotencia, string Texto);

public sealed record MarcarLecturaRequest(long HastaSecuencia);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportarChatRequest(
    TipoObjetivoReporteChat TipoObjetivo,
    Guid? MensajeId,
    CategoriaReporteChat Categoria,
    string? Detalle);

public sealed record PaginaChatResponse<T>(IReadOnlyList<T> Items, string? SiguienteCursor);

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/chat").RequireAuthorization();

        grupo.MapPost("/conversaciones", IniciarAsync)
            .RequireRateLimiting("chat-iniciar")
            .Produces<ConversacionDto>(StatusCodes.Status200OK)
            .Produces<ConversacionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/conversaciones", ListarAsync)
            .RequireRateLimiting("chat-consultas")
            .Produces<PaginaChatResponse<ConversacionResumenDto>>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/conversaciones/{id:guid}/mensajes", EnviarAsync)
            .RequireRateLimiting("chat-enviar")
            .Produces<MensajeDto>(StatusCodes.Status200OK)
            .Produces<MensajeDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/conversaciones/{id:guid}/mensajes", ObtenerMensajesAsync)
            .RequireRateLimiting("chat-consultas")
            .Produces<PaginaChatResponse<MensajeDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPut("/conversaciones/{id:guid}/lectura", MarcarLecturaAsync)
            .RequireRateLimiting("chat-consultas")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/conversaciones/{id:guid}/reportes", ReportarAsync)
            .RequireRateLimiting("chat-seguridad-acciones")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPut("/conversaciones/{id:guid}/cierre", CerrarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapDelete("/conversaciones/{id:guid}/cierre", ReabrirAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPut("/conversaciones/{id:guid}/bloqueo", BloquearAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapDelete("/conversaciones/{id:guid}/bloqueo", DesbloquearAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> IniciarAsync(
        IniciarConversacionRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var ejecucion = await EjecutarConReintentoAsync(
                () => sender.Send(new IniciarConversacionCommand(usuarioId, request.AvisoId), ct));
            if (ejecucion.Conflicto)
            {
                return ConflictoPersistencia();
            }

            var resultado = ejecucion.Valor!;
            if (!resultado.EsExito)
            {
                return DesdeError(resultado.Error);
            }

            return resultado.Valor.FueCreada
                ? Results.Created(
                    $"/api/chat/conversaciones/{resultado.Valor.Conversacion.Id}",
                    resultado.Valor.Conversacion)
                : Results.Ok(resultado.Valor.Conversacion);
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
        string? cursor = null,
        int limite = 20)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        FronteraConversaciones? frontera = null;
        if (cursor is not null)
        {
            if (!CursoresChat.TryDecodificarConversaciones(cursor, out var valor))
            {
                return CursorInvalido();
            }

            frontera = valor;
        }

        try
        {
            var pagina = await sender.Send(
                new ListarConversacionesQuery(usuarioId, frontera, limite), ct);
            return Results.Ok(new PaginaChatResponse<ConversacionResumenDto>(
                pagina.Items,
                pagina.Siguiente is { } siguiente ? CursoresChat.Codificar(siguiente) : null));
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> EnviarAsync(
        Guid id,
        EnviarMensajeRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var ejecucion = await EjecutarConReintentoAsync(
                () => sender.Send(new EnviarMensajeCommand(
                    id, usuarioId, request.ClaveIdempotencia, request.Texto), ct));
            if (ejecucion.Conflicto)
            {
                return ConflictoPersistencia();
            }

            var resultado = ejecucion.Valor!;
            if (!resultado.EsExito)
            {
                return DesdeError(resultado.Error);
            }

            return resultado.Valor.FueCreado
                ? Results.Created(
                    $"/api/chat/conversaciones/{id}/mensajes/{resultado.Valor.Mensaje.Id}",
                    resultado.Valor.Mensaje)
                : Results.Ok(resultado.Valor.Mensaje);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ObtenerMensajesAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct,
        string? cursor = null,
        long? despuesDeSecuencia = null,
        int limite = 50)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        long? antesDe = null;
        if (cursor is not null && despuesDeSecuencia.HasValue)
        {
            return CursorInvalido();
        }

        if (cursor is not null)
        {
            if (!CursoresChat.TryDecodificarMensajes(cursor, out var valor))
            {
                return CursorInvalido();
            }

            antesDe = valor;
        }

        try
        {
            var resultado = await sender.Send(
                new ObtenerMensajesQuery(id, usuarioId, antesDe, despuesDeSecuencia, limite), ct);
            if (!resultado.EsExito)
            {
                return DesdeError(resultado.Error);
            }

            return Results.Ok(new PaginaChatResponse<MensajeDto>(
                resultado.Valor.Items,
                resultado.Valor.Siguiente is { } siguiente ? CursoresChat.Codificar(siguiente) : null));
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> MarcarLecturaAsync(
        Guid id,
        MarcarLecturaRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var ejecucion = await EjecutarConReintentoAsync(
                () => sender.Send(new MarcarLecturaCommand(id, usuarioId, request.HastaSecuencia), ct));
            if (ejecucion.Conflicto)
            {
                return ConflictoPersistencia();
            }

            return ejecucion.Valor!.EsExito
                ? Results.NoContent()
                : DesdeError(ejecucion.Valor.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ReportarAsync(
        Guid id,
        ReportarChatRequest request,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var ejecucion = await EjecutarConReintentoAsync(
                () => sender.Send(new ReportarChatCommand(
                    id,
                    usuarioId,
                    request.TipoObjetivo,
                    request.MensajeId,
                    request.Categoria,
                    request.Detalle), ct));
            if (ejecucion.Conflicto)
            {
                return ConflictoPersistencia();
            }

            var resultado = ejecucion.Valor!;
            if (!resultado.EsExito)
            {
                return DesdeError(resultado.Error);
            }

            return Results.Created(
                $"/api/chat/conversaciones/{id}/reportes/{resultado.Valor}",
                new { id = resultado.Valor });
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> CerrarAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var ejecucion = await EjecutarConReintentoAsync(
            () => sender.Send(new CerrarConversacionCommand(id, usuarioId), ct));
        if (ejecucion.Conflicto)
        {
            return ConflictoPersistencia();
        }

        return ejecucion.Valor!.EsExito
            ? Results.NoContent()
            : DesdeError(ejecucion.Valor.Error);
    }

    private static async Task<IResult> ReabrirAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var ejecucion = await EjecutarConReintentoAsync(
            () => sender.Send(new ReabrirConversacionCommand(id, usuarioId), ct));
        if (ejecucion.Conflicto)
        {
            return ConflictoPersistencia();
        }

        return ejecucion.Valor!.EsExito
            ? Results.NoContent()
            : DesdeError(ejecucion.Valor.Error);
    }

    private static async Task<IResult> BloquearAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var ejecucion = await EjecutarConReintentoAsync(
            () => sender.Send(new BloquearUsuarioCommand(id, usuarioId), ct));
        if (ejecucion.Conflicto)
        {
            return ConflictoPersistencia();
        }

        return ejecucion.Valor!.EsExito
            ? Results.NoContent()
            : DesdeError(ejecucion.Valor.Error);
    }

    private static async Task<IResult> DesbloquearAsync(
        Guid id,
        ClaimsPrincipal usuario,
        ISender sender,
        CancellationToken ct)
    {
        if (!TryUserId(usuario, out var usuarioId))
        {
            return Results.Unauthorized();
        }

        var ejecucion = await EjecutarConReintentoAsync(
            () => sender.Send(new DesbloquearUsuarioCommand(id, usuarioId), ct));
        if (ejecucion.Conflicto)
        {
            return ConflictoPersistencia();
        }

        return ejecucion.Valor!.EsExito
            ? Results.NoContent()
            : DesdeError(ejecucion.Valor.Error);
    }

    private static async Task<(T? Valor, bool Conflicto)> EjecutarConReintentoAsync<T>(
        Func<Task<T>> accion)
        where T : class
    {
        for (var intento = 0; intento < 2; intento++)
        {
            try
            {
                return (await accion(), false);
            }
            catch (Exception ex) when (EsConflictoRecuperable(ex))
            {
                if (intento == 1)
                {
                    return (null, true);
                }
            }
        }

        return (null, true);
    }

    private static bool EsConflictoRecuperable(Exception ex) =>
        ex is ConflictoUnicidadChatException or ConflictoConcurrenciaException;

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    private static IResult CursorInvalido() => Results.Problem(
        title: "chat_cursor_invalido",
        detail: "El cursor no es válido.",
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult ConflictoPersistencia() => Results.Problem(
        title: "chat_conflicto_concurrencia",
        detail: "No se pudo completar la operación. Inténtelo nuevamente.",
        statusCode: StatusCodes.Status409Conflict);

    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresConversacion.NoEncontrada or ErroresConversacion.AvisoNoContactable =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        ErroresConversacion.ParticipantesCoinciden or ErroresConversacion.ClaveIdempotenciaReutilizada
            or ErroresConversacion.NoDisponibleParaEnvio
            or ErroresModeracionChat.TransicionInvalida =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
