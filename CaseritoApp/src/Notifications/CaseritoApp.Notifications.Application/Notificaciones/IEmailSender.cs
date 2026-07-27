namespace CaseritoApp.Notifications.Application.Notificaciones;

public interface IEmailSender
{
    public Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoTexto,
        string? cuerpoHtml = null,
        CancellationToken ct = default);
}
