namespace CaseritoApp.Host.Chat;

public interface IRevocadorTiempoRealChat
{
    public Task RevocarAsync(Guid conversacionId, CancellationToken cancellationToken);

    public Task RevocarConversacionesCompartidasAsync(
        Guid conversacionId,
        CancellationToken cancellationToken);

    public Task RevocarPorReporteAsync(Guid reporteId, CancellationToken cancellationToken);
}
