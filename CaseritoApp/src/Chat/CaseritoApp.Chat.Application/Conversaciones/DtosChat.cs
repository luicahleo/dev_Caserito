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
    bool PuedeEnviar,
    long UltimaSecuenciaEntregadaContraparte = 0,
    long UltimaSecuenciaLeidaContraparte = 0)
{
    public static ConversacionDto Desde(Conversacion conversacion) =>
        Desde(conversacion, conversacion.Estado == EstadoConversacion.Activa);

    public static ConversacionDto Desde(
        Conversacion conversacion,
        bool puedeEnviar,
        Guid? participanteId = null)
    {
        var entregaContraparte = 0L;
        var lecturaContraparte = 0L;
        if (participanteId == conversacion.CompradorId)
        {
            entregaContraparte = conversacion.UltimaSecuenciaEntregadaVendedor;
            lecturaContraparte = conversacion.UltimaSecuenciaLeidaVendedor;
        }
        else if (participanteId == conversacion.VendedorId)
        {
            entregaContraparte = conversacion.UltimaSecuenciaEntregadaComprador;
            lecturaContraparte = conversacion.UltimaSecuenciaLeidaComprador;
        }

        return new ConversacionDto(
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
            puedeEnviar,
            entregaContraparte,
            lecturaContraparte);
    }
}

public sealed record IniciarConversacionResultadoDto(
    ConversacionDto Conversacion,
    bool FueCreada);
