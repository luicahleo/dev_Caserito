using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.Chat.Application.Moderacion;

public sealed record TomarReporteChatCommand(Guid ReporteId, Guid ModeradorId) : ICommand;
public sealed record LiberarReporteChatCommand(Guid ReporteId, Guid ModeradorId) : ICommand;
public sealed record AtenderReporteChatCommand(Guid ReporteId, Guid ModeradorId, bool CerrarConversacion) : ICommand;
public sealed record DescartarReporteChatCommand(Guid ReporteId, Guid ModeradorId) : ICommand;
public sealed record CerrarPorModeracionCommand(Guid ReporteId, Guid ModeradorId) : ICommand;
public sealed record ReabrirPorModeracionCommand(Guid ReporteId, Guid ModeradorId) : ICommand;

public sealed class TomarReporteChatCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    TimeProvider reloj) : ICommandHandler<TomarReporteChatCommand>
{
    public Task<Result> Handle(TomarReporteChatCommand request, CancellationToken cancellationToken) =>
        WorkflowModeracionChat.EjecutarReporteAsync(
            request.ReporteId, request.ModeradorId, AccionModeracionChat.Tomar,
            (r, f) => r.Tomar(request.ModeradorId, f), reportes, registros, reloj, cancellationToken);
}

public sealed class LiberarReporteChatCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    TimeProvider reloj) : ICommandHandler<LiberarReporteChatCommand>
{
    public Task<Result> Handle(LiberarReporteChatCommand request, CancellationToken cancellationToken) =>
        WorkflowModeracionChat.EjecutarReporteAsync(
            request.ReporteId, request.ModeradorId, AccionModeracionChat.Liberar,
            (r, _) => r.Liberar(request.ModeradorId), reportes, registros, reloj, cancellationToken);
}

public sealed class DescartarReporteChatCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    TimeProvider reloj) : ICommandHandler<DescartarReporteChatCommand>
{
    public Task<Result> Handle(DescartarReporteChatCommand request, CancellationToken cancellationToken) =>
        WorkflowModeracionChat.EjecutarReporteAsync(
            request.ReporteId, request.ModeradorId, AccionModeracionChat.Descartar,
            (r, f) => r.Descartar(request.ModeradorId, f), reportes, registros, reloj, cancellationToken);
}

public sealed class AtenderReporteChatCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    IRepositorioConversaciones conversaciones,
    TimeProvider reloj) : ICommandHandler<AtenderReporteChatCommand>
{
    public async Task<Result> Handle(AtenderReporteChatCommand request, CancellationToken cancellationToken)
    {
        var reporte = await reportes.ObtenerAsync(request.ReporteId, cancellationToken);
        if (reporte is null)
        {
            return WorkflowModeracionChat.NoEncontrado();
        }

        if (reporte.Estado != EstadoReporteChat.EnRevision
            || reporte.ModeradorAsignadoId != request.ModeradorId)
        {
            return Result.Fallo(new Error(
                ErroresModeracionChat.TransicionInvalida,
                "El reporte no está disponible para esa acción."));
        }

        var fecha = reloj.GetUtcNow();
        if (request.CerrarConversacion)
        {
            var conversacion = await conversaciones.ObtenerAsync(reporte.ConversacionId, cancellationToken);
            if (conversacion is null)
            {
                return WorkflowModeracionChat.NoEncontrado();
            }

            var cierre = conversacion.CerrarPorModeracion(request.ModeradorId, fecha);
            if (!cierre.EsExito)
            {
                return cierre;
            }

            registros.Agregar(WorkflowModeracionChat.CrearRegistro(
                reporte, request.ModeradorId, AccionModeracionChat.CerrarConversacion, fecha));
        }

        var resultado = reporte.Atender(request.ModeradorId, fecha);
        if (!resultado.EsExito)
        {
            return resultado;
        }

        registros.Agregar(WorkflowModeracionChat.CrearRegistro(
            reporte, request.ModeradorId, AccionModeracionChat.Atender, fecha));
        return Result.Exito();
    }
}

public sealed class CerrarPorModeracionCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    IRepositorioConversaciones conversaciones,
    TimeProvider reloj) : ICommandHandler<CerrarPorModeracionCommand>
{
    public Task<Result> Handle(CerrarPorModeracionCommand request, CancellationToken cancellationToken) =>
        WorkflowModeracionChat.CambiarEstadoConversacionAsync(
            request.ReporteId, request.ModeradorId, AccionModeracionChat.CerrarConversacion,
            (c, f) => c.CerrarPorModeracion(request.ModeradorId, f),
            reportes, registros, conversaciones, reloj, cancellationToken);
}

public sealed class ReabrirPorModeracionCommandHandler(
    IRepositorioReportesChat reportes,
    IRepositorioRegistrosModeracionChat registros,
    IRepositorioConversaciones conversaciones,
    TimeProvider reloj) : ICommandHandler<ReabrirPorModeracionCommand>
{
    public Task<Result> Handle(ReabrirPorModeracionCommand request, CancellationToken cancellationToken) =>
        WorkflowModeracionChat.CambiarEstadoConversacionAsync(
            request.ReporteId, request.ModeradorId, AccionModeracionChat.ReabrirConversacion,
            (c, f) => c.ReabrirPorModeracion(request.ModeradorId, f),
            reportes, registros, conversaciones, reloj, cancellationToken);
}

internal static class WorkflowModeracionChat
{
    internal static async Task<Result> EjecutarReporteAsync(
        Guid reporteId, Guid moderadorId, AccionModeracionChat accion,
        Func<ReporteChat, DateTimeOffset, Result> transicion,
        IRepositorioReportesChat reportes, IRepositorioRegistrosModeracionChat registros,
        TimeProvider reloj, CancellationToken ct)
    {
        var reporte = await reportes.ObtenerAsync(reporteId, ct);
        if (reporte is null)
        {
            return NoEncontrado();
        }

        var fecha = reloj.GetUtcNow();
        var resultado = transicion(reporte, fecha);
        if (!resultado.EsExito)
        {
            return resultado;
        }

        registros.Agregar(CrearRegistro(reporte, moderadorId, accion, fecha));
        return Result.Exito();
    }

    internal static async Task<Result> CambiarEstadoConversacionAsync(
        Guid reporteId, Guid moderadorId, AccionModeracionChat accion,
        Func<Domain.Conversaciones.Conversacion, DateTimeOffset, Result> transicion,
        IRepositorioReportesChat reportes, IRepositorioRegistrosModeracionChat registros,
        IRepositorioConversaciones conversaciones, TimeProvider reloj, CancellationToken ct)
    {
        var reporte = await reportes.ObtenerAsync(reporteId, ct);
        if (reporte is null || reporte.ModeradorAsignadoId != moderadorId)
        {
            return NoEncontrado();
        }

        var conversacion = await conversaciones.ObtenerAsync(reporte.ConversacionId, ct);
        if (conversacion is null)
        {
            return NoEncontrado();
        }

        var fecha = reloj.GetUtcNow();
        var resultado = transicion(conversacion, fecha);
        if (!resultado.EsExito)
        {
            return resultado;
        }

        registros.Agregar(CrearRegistro(reporte, moderadorId, accion, fecha));
        return Result.Exito();
    }

    internal static RegistroModeracionChat CrearRegistro(
        ReporteChat reporte, Guid moderadorId, AccionModeracionChat accion, DateTimeOffset fecha) =>
        RegistroModeracionChat.Crear(reporte.Id, reporte.ConversacionId, moderadorId, accion, fecha).Valor;

    internal static Result NoEncontrado() => Result.Fallo(new Error(
        ErroresModeracionChat.NoEncontrado, "El reporte no está disponible."));
}
