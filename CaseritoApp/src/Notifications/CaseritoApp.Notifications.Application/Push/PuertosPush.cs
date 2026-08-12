using CaseritoApp.Notifications.Domain.Push;

namespace CaseritoApp.Notifications.Application.Push;

public interface IRepositorioSuscripcionesPush
{
    public Task<SuscripcionPush?> ObtenerPorDispositivoAsync(
        Guid usuarioId, string dispositivoId, CancellationToken ct);
    public Task<IReadOnlyList<SuscripcionPush>> ListarActivasAsync(Guid usuarioId, CancellationToken ct);
    public void Agregar(SuscripcionPush suscripcion);
}

public interface IAlmacenIntencionesPush
{
    public Task<bool> ExisteEventoAsync(Guid eventoId, CancellationToken ct);
    public void Agregar(IntencionPush intencion);
    public Task<IReadOnlyList<IntencionPush>> ReclamarAsync(
        int maximo, DateTimeOffset ahora, TimeSpan lease, CancellationToken ct);
}

public enum ResultadoEnvioPush
{
    Exito,
    SuscripcionExpirada,
    ErrorTransitorio
}

public sealed record EnvioPush(
    string Endpoint,
    string P256dh,
    string Auth,
    string Payload);

public interface IWebPushSender
{
    public Task<ResultadoEnvioPush> EnviarAsync(EnvioPush envio, CancellationToken ct);
}

public sealed record DatosComprobanteEntrega(
    Guid ConversacionId,
    Guid DestinatarioId,
    long Secuencia);

public interface IProtectorComprobantesEntrega
{
    public string Crear(DatosComprobanteEntrega datos, DateTimeOffset expiraEn);
    public bool TryValidar(string comprobante, out DatosComprobanteEntrega datos);
}
