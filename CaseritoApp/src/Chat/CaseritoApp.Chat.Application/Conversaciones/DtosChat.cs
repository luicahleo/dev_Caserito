using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record ConversacionDto(
    Guid Id,
    Guid AvisoId,
    Guid CompradorId,
    Guid VendedorId,
    DateTimeOffset CreadaEn,
    DateTimeOffset UltimaActividadEn,
    long UltimaSecuencia,
    EstadoConversacion Estado,
    string? OrigenCierre,
    bool PuedeEnviar)
{
    public static ConversacionDto Desde(Conversacion conversacion) =>
        Desde(conversacion, conversacion.Estado == EstadoConversacion.Activa);

    public static ConversacionDto Desde(Conversacion conversacion, bool puedeEnviar) => new(
        conversacion.Id,
        conversacion.AvisoId,
        conversacion.CompradorId,
        conversacion.VendedorId,
        conversacion.CreadaEn,
        conversacion.UltimaActividadEn,
        conversacion.UltimaSecuencia,
        conversacion.Estado,
        conversacion.Estado switch
        {
            EstadoConversacion.Cerrada => "Participante",
            EstadoConversacion.CerradaPorModeracion => "Moderacion",
            _ => null,
        },
        puedeEnviar);
}

public sealed record IniciarConversacionResultadoDto(
    ConversacionDto Conversacion,
    bool FueCreada);
