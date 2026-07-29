using CaseritoApp.Identity.Application.Correo;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed partial class ServicioCorreoSmtp(
    IOptions<OpcionesCorreo> opciones,
    ILogger<ServicioCorreoSmtp> logger) : IServicioCorreo
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.Remitente));
        mime.To.Add(MailboxAddress.Parse(mensaje.Para));
        mime.Subject = mensaje.Asunto;

        var bodyBuilder = new BodyBuilder { TextBody = mensaje.CuerpoTexto };
        if (!string.IsNullOrWhiteSpace(mensaje.CuerpoHtml))
        {
            bodyBuilder.HtmlBody = mensaje.CuerpoHtml;
        }

        mime.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_opciones.Host, _opciones.Puerto, SecureSocketOptions.StartTls, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            RegistrarEnvio(logger, mensaje.Para, mensaje.Asunto);
        }
        catch (Exception ex)
        {
            RegistrarError(logger, mensaje.Para, mensaje.Asunto, ex.Message);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo enviado: para={Para} asunto={Asunto}")]
    private static partial void RegistrarEnvio(ILogger logger, string para, string asunto);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error enviando correo: para={Para} asunto={Asunto} error={Error}")]
    private static partial void RegistrarError(ILogger logger, string para, string asunto, string error);
}
