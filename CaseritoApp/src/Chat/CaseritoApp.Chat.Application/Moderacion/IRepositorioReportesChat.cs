using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.Chat.Application.Moderacion;

public interface IRepositorioReportesChat
{
    public Task<bool> ExisteAbiertoAsync(
        Guid conversacionId,
        Guid reportanteId,
        TipoObjetivoReporteChat tipoObjetivo,
        Guid? mensajeId,
        CancellationToken ct);

    public void Agregar(ReporteChat reporte);
}
