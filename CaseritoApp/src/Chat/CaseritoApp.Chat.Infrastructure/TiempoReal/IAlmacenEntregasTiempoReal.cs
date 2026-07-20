namespace CaseritoApp.Chat.Infrastructure.TiempoReal;

public interface IAlmacenEntregasTiempoReal
{
    public Task<IReadOnlyList<EntregaTiempoRealReclamada>> ReclamarAsync(
        int cantidadMaxima,
        TimeSpan duracionLease,
        CancellationToken cancellationToken);

    public Task<bool> MarcarProcesadaAsync(
        EntregaTiempoRealReclamada entrega,
        CancellationToken cancellationToken);

    public Task<bool> ReprogramarAsync(
        EntregaTiempoRealReclamada entrega,
        CancellationToken cancellationToken);
}

public sealed record EntregaTiempoRealReclamada(
    Guid EntregaId,
    DateTimeOffset LeaseHasta,
    int Intentos,
    MensajeEntregaTiempoReal Mensaje);

public sealed record MensajeEntregaTiempoReal(
    Guid Id,
    Guid ConversacionId,
    Guid RemitenteId,
    long Secuencia,
    string Texto,
    DateTimeOffset EnviadoEn);
