using CaseritoApp.Notifications.Application.Notificaciones;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Notifications.Infrastructure.Email;

public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger)
    : IEmailSender
{
    public Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoTexto,
        string? cuerpoHtml = null,
        CancellationToken ct = default)
    {
        LogEmailEnviado(logger, asunto);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email enviado a destinatario oculto: asunto={Asunto}")]
    private static partial void LogEmailEnviado(ILogger logger, string asunto);
}
