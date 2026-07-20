namespace CaseritoApp.Host.Chat;

public interface IPublicadorMensajesTiempoReal
{
    public Task PublicarAsync(
        MensajeTiempoRealDto mensaje,
        CancellationToken cancellationToken);
}
