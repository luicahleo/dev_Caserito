using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Domain.Push;

public sealed class IntencionPush : AggregateRoot
{
    private IntencionPush()
    {
    }

    private IntencionPush(
        Guid eventoId,
        Guid destinatarioId,
        Guid conversacionId,
        long secuencia,
        DateTimeOffset ahora)
    {
        EventoId = eventoId;
        DestinatarioId = destinatarioId;
        ConversacionId = conversacionId;
        Secuencia = secuencia;
        CreadaEn = ahora.ToUniversalTime();
        DisponibleEn = CreadaEn;
    }

    public Guid EventoId { get; private set; }
    public Guid DestinatarioId { get; private set; }
    public Guid ConversacionId { get; private set; }
    public long Secuencia { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset DisponibleEn { get; private set; }
    public DateTimeOffset? LeaseHasta { get; private set; }
    public DateTimeOffset? ProcesadaEn { get; private set; }
    public int Intentos { get; private set; }
    public bool Procesada => ProcesadaEn.HasValue;

    public static Result<IntencionPush> Crear(
        Guid eventoId,
        Guid destinatarioId,
        Guid conversacionId,
        long secuencia,
        DateTimeOffset ahora)
    {
        if (eventoId == Guid.Empty || destinatarioId == Guid.Empty
            || conversacionId == Guid.Empty || secuencia <= 0)
        {
            return Result.Fallo<IntencionPush>(new Error(
                "Notifications.Push.IntencionInvalida", "No fue posible crear el aviso."));
        }

        return Result.Exito(new IntencionPush(
            eventoId, destinatarioId, conversacionId, secuencia, ahora));
    }

    public bool Reclamar(DateTimeOffset ahora, TimeSpan duracion)
    {
        var instante = ahora.ToUniversalTime();
        if (Procesada || DisponibleEn > instante || LeaseHasta > instante || duracion <= TimeSpan.Zero)
        {
            return false;
        }

        LeaseHasta = instante.Add(duracion);
        return true;
    }

    public void Reprogramar(DateTimeOffset ahora, TimeSpan demora)
    {
        Intentos++;
        LeaseHasta = null;
        DisponibleEn = ahora.ToUniversalTime().Add(demora);
    }

    public void MarcarProcesada(DateTimeOffset ahora)
    {
        ProcesadaEn ??= ahora.ToUniversalTime();
        LeaseHasta = null;
    }
}
