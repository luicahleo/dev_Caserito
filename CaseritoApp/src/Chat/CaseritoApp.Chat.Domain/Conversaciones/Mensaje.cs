using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Conversaciones;

public sealed class Mensaje : Entity
{
    private Mensaje()
    {
        Texto = null!;
    }

    internal Mensaje(
        Guid conversacionId,
        Guid remitenteId,
        Guid claveIdempotencia,
        long secuencia,
        string texto,
        DateTimeOffset enviadoEn)
    {
        ConversacionId = conversacionId;
        RemitenteId = remitenteId;
        ClaveIdempotencia = claveIdempotencia;
        Secuencia = secuencia;
        Texto = texto;
        EnviadoEn = enviadoEn;
    }

    public Guid ConversacionId { get; private set; }

    public Guid RemitenteId { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public long Secuencia { get; private set; }

    public string Texto { get; private set; }

    public DateTimeOffset EnviadoEn { get; private set; }
}
