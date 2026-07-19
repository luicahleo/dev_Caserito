using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;

namespace CaseritoApp.Catalog.Application.Moderacion;

public sealed record OcultarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record RestaurarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record EliminarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record DescartarReporteAvisoCommand(Guid ReporteId, Guid ModeradorId) : ICommand;

public sealed class OcultarAvisoPorModeracionCommandHandler(
    IRepositorioAvisos avisos, IRepositorioReportesAviso reportes,
    IRepositorioRegistrosModeracion registros, TimeProvider reloj)
    : ICommandHandler<OcultarAvisoPorModeracionCommand>
{
    public async Task<Result> Handle(OcultarAvisoPorModeracionCommand request, CancellationToken cancellationToken) =>
        await AccionesModeracion.EjecutarSobreAvisoAsync(
            request.AvisoId, request.ModeradorId, AccionModeracionAviso.Ocultar,
            (a, f) => a.OcultarPorModeracion(f), avisos, reportes, registros, reloj, cancellationToken);
}

public sealed class RestaurarAvisoPorModeracionCommandHandler(
    IRepositorioAvisos avisos, IRepositorioReportesAviso reportes,
    IRepositorioRegistrosModeracion registros, TimeProvider reloj)
    : ICommandHandler<RestaurarAvisoPorModeracionCommand>
{
    public async Task<Result> Handle(RestaurarAvisoPorModeracionCommand request, CancellationToken cancellationToken) =>
        await AccionesModeracion.EjecutarSobreAvisoAsync(
            request.AvisoId, request.ModeradorId, AccionModeracionAviso.Restaurar,
            (a, f) => a.RestaurarPorModeracion(f), avisos, reportes, registros, reloj, cancellationToken);
}

public sealed class EliminarAvisoPorModeracionCommandHandler(
    IRepositorioAvisos avisos, IRepositorioReportesAviso reportes,
    IRepositorioRegistrosModeracion registros, TimeProvider reloj)
    : ICommandHandler<EliminarAvisoPorModeracionCommand>
{
    public async Task<Result> Handle(EliminarAvisoPorModeracionCommand request, CancellationToken cancellationToken) =>
        await AccionesModeracion.EjecutarSobreAvisoAsync(
            request.AvisoId, request.ModeradorId, AccionModeracionAviso.Eliminar,
            (a, f) => a.EliminarPorModeracion(f), avisos, reportes, registros, reloj, cancellationToken);
}

public sealed class DescartarReporteAvisoCommandHandler(
    IRepositorioReportesAviso reportes, IRepositorioRegistrosModeracion registros, TimeProvider reloj)
    : ICommandHandler<DescartarReporteAvisoCommand>
{
    public async Task<Result> Handle(DescartarReporteAvisoCommand request, CancellationToken cancellationToken)
    {
        var reporte = await reportes.ObtenerAsync(request.ReporteId, cancellationToken);
        if (reporte is null)
        {
            return Result.Fallo(new Error(ErroresAviso.ReporteNoEncontrado, "El reporte no existe."));
        }
        var fecha = reloj.GetUtcNow().UtcDateTime;
        var resultado = reporte.Descartar(request.ModeradorId, fecha);
        if (!resultado.EsExito)
        {
            return resultado;
        }
        registros.Agregar(RegistroModeracion.Crear(
            reporte.AvisoId, request.ModeradorId, AccionModeracionAviso.DescartarReporte, reporte.Id, fecha));
        return Result.Exito();
    }
}

internal static class AccionesModeracion
{
    public static async Task<Result> EjecutarSobreAvisoAsync(
        Guid avisoId, Guid moderadorId, AccionModeracionAviso accion,
        Func<Aviso, DateTime, Result> transicion, IRepositorioAvisos avisos,
        IRepositorioReportesAviso reportes, IRepositorioRegistrosModeracion registros,
        TimeProvider reloj, CancellationToken ct)
    {
        var aviso = await avisos.ObtenerAsync(avisoId, ct);
        if (aviso is null)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }
        var fecha = reloj.GetUtcNow().UtcDateTime;
        var resultado = transicion(aviso, fecha);
        if (!resultado.EsExito)
        {
            return resultado;
        }
        if (accion is AccionModeracionAviso.Ocultar or AccionModeracionAviso.Eliminar)
        {
            foreach (var reporte in await reportes.ListarPendientesAsync(avisoId, ct))
            {
                reporte.Atender(moderadorId, fecha);
            }
        }
        registros.Agregar(RegistroModeracion.Crear(avisoId, moderadorId, accion, null, fecha));
        return Result.Exito();
    }
}
