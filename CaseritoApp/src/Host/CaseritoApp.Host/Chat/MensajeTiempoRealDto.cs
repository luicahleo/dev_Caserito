namespace CaseritoApp.Host.Chat;

public sealed record MensajeTiempoRealDto(
    Guid Id,
    Guid ConversacionId,
    Guid RemitenteId,
    long Secuencia,
    string Texto,
    DateTimeOffset EnviadoEn);
