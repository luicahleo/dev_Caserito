using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Moderacion;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Elimina (soft-delete) un aviso propio.</summary>
public sealed record EliminarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="EliminarAvisoCommand"/>.</summary>
public sealed class EliminarAvisoCommandHandler(
    IRepositorioAvisos repositorio,
    TimeProvider reloj,
    IRepositorioReportesAviso? reportes = null)
    : ICommandHandler<EliminarAvisoCommand>
{
    public async Task<Result> Handle(EliminarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var resultado = aviso.Eliminar(ahora);
        if (!resultado.EsExito || reportes is null)
        {
            return resultado;
        }

        var repositorioReportes = reportes;
        var pendientes = await repositorioReportes.ListarPendientesAsync(aviso.Id, cancellationToken);
        foreach (var reporte in pendientes)
        {
            reporte.Atender(null, ahora);
        }

        return resultado;
    }
}
