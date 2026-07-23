using MediatR;

namespace CaseritoApp.Chat.Application.Seguridad;

public sealed record AccesoTiempoRealRevocado(Guid ConversacionId) : INotification;

public sealed record BloqueoTiempoRealConfirmado(Guid ConversacionId) : INotification;

public sealed record AccesoTiempoRealRevocadoPorReporte(Guid ReporteId) : INotification;
