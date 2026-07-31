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
            var seguridad = _opciones.HabilitarSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            await client.ConnectAsync(_opciones.Host, _opciones.Puerto, seguridad, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            RegistrarEnvio(logger);
        }
        catch (Exception ex)
        {
            RegistrarError(logger, _opciones.Host, _opciones.Puerto, _opciones.HabilitarSsl, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo enviado correctamente.")]
    private static partial void RegistrarEnvio(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo en ServicioCorreoSmtp.EnviarAsync: host={Host} puerto={Puerto} startTls={StartTls}")]
    private static partial void RegistrarError(
        ILogger logger,
        string host,
        int puerto,
        bool startTls,
        Exception ex);
}
