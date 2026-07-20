using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Infrastructure.TiempoReal;

public sealed class EntregaTiempoReal
{
    private EntregaTiempoReal()
    {
    }

    private EntregaTiempoReal(Mensaje mensaje)
    {
        Id = Guid.NewGuid();
        ConversacionId = mensaje.ConversacionId;
        MensajeId = mensaje.Id;
        Secuencia = mensaje.Secuencia;
        CreadaEn = mensaje.EnviadoEn;
        ProximoIntentoEn = mensaje.EnviadoEn;
    }

    public Guid Id { get; private set; }

    public Guid ConversacionId { get; private set; }

    public Guid MensajeId { get; private set; }

    public long Secuencia { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public int Intentos { get; private set; }

    public DateTimeOffset ProximoIntentoEn { get; private set; }

    public DateTimeOffset? ProcesadaEn { get; private set; }

    public DateTimeOffset? LeaseHasta { get; private set; }

    internal static EntregaTiempoReal Para(Mensaje mensaje) => new(mensaje);

    internal void AsignarLease(DateTimeOffset hasta) => LeaseHasta = hasta;

    internal void MarcarProcesada(DateTimeOffset ahora)
    {
        ProcesadaEn = ahora;
        LeaseHasta = null;
    }

    internal void Reprogramar(DateTimeOffset proximoIntento)
    {
        Intentos++;
        ProximoIntentoEn = proximoIntento;
        LeaseHasta = null;
    }
}
