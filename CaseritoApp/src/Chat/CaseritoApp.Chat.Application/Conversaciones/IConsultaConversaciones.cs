using CaseritoApp.Chat.Application.Paginacion;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Conversaciones;

public readonly record struct FronteraConversaciones(
    DateTimeOffset UltimaActividadEn,
    Guid ConversacionId);

public sealed record ConversacionResumenDto(
    Guid Id,
    Guid AvisoId,
    Guid ContraparteId,
    string Rol,
    DateTimeOffset CreadaEn,
    DateTimeOffset UltimaActividadEn,
    long UltimaSecuencia,
    int NoLeidos,
    EstadoConversacion Estado,
    string? OrigenCierre,
    bool PuedeEnviar);

public interface IConsultaConversaciones
{
    public Task<bool> PuedeAccederAsync(
        Guid conversacionId,
        Guid usuarioId,
        CancellationToken ct);

    public Task<PaginaCursor<ConversacionResumenDto, FronteraConversaciones>> ListarAsync(
        Guid usuarioId,
        FronteraConversaciones? frontera,
        int limite,
        CancellationToken ct);
}
