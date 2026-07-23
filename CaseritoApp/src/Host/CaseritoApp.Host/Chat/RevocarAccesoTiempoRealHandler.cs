using CaseritoApp.Chat.Application.Seguridad;
using MediatR;

namespace CaseritoApp.Host.Chat;

public sealed class RevocarAccesoTiempoRealHandler(IRevocadorTiempoRealChat revocador)
    : INotificationHandler<AccesoTiempoRealRevocado>,
      INotificationHandler<BloqueoTiempoRealConfirmado>,
      INotificationHandler<AccesoTiempoRealRevocadoPorReporte>
{
    public Task Handle(
        AccesoTiempoRealRevocado notification,
        CancellationToken cancellationToken) =>
        revocador.RevocarAsync(notification.ConversacionId, cancellationToken);

    public Task Handle(
        BloqueoTiempoRealConfirmado notification,
        CancellationToken cancellationToken) =>
        revocador.RevocarConversacionesCompartidasAsync(
            notification.ConversacionId,
            cancellationToken);

    public Task Handle(
        AccesoTiempoRealRevocadoPorReporte notification,
        CancellationToken cancellationToken) =>
        revocador.RevocarPorReporteAsync(notification.ReporteId, cancellationToken);
}
