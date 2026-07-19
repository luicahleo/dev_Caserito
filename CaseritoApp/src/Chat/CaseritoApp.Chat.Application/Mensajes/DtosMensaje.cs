using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Mensajes;

public sealed record MensajeDto(
    Guid Id,
    Guid ConversacionId,
    Guid RemitenteId,
    long Secuencia,
    string Texto,
    DateTimeOffset EnviadoEn)
{
    public static MensajeDto Desde(Mensaje mensaje) => new(
        mensaje.Id,
        mensaje.ConversacionId,
        mensaje.RemitenteId,
        mensaje.Secuencia,
        mensaje.Texto,
        mensaje.EnviadoEn);
}

public sealed record EnviarMensajeResultadoDto(MensajeDto Mensaje, bool FueCreado);
