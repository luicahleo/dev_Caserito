namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record NotificacionDto(
    Guid Id,
    string Tipo,
    string Titulo,
    string Mensaje,
    Guid? EntidadRelacionadaId,
    bool Leida,
    DateTimeOffset CreadaEn);

public sealed record PaginaNotificacionesDto(
    IReadOnlyList<NotificacionDto> Items,
    int Pagina,
    int Tamano,
    int Total);
