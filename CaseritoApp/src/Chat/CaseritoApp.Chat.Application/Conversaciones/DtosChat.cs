using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record ConversacionDto(
    Guid Id,
    Guid AvisoId,
    Guid CompradorId,
    Guid VendedorId,
    DateTimeOffset CreadaEn,
    DateTimeOffset UltimaActividadEn,
    long UltimaSecuencia)
{
    public static ConversacionDto Desde(Conversacion conversacion) => new(
        conversacion.Id,
        conversacion.AvisoId,
        conversacion.CompradorId,
        conversacion.VendedorId,
        conversacion.CreadaEn,
        conversacion.UltimaActividadEn,
        conversacion.UltimaSecuencia);
}

public sealed record IniciarConversacionResultadoDto(
    ConversacionDto Conversacion,
    bool FueCreada);
