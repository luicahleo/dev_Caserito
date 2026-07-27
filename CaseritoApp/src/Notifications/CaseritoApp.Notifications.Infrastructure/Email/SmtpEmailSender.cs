using System.Net;
using System.Net.Mail;
using CaseritoApp.Notifications.Application.Notificaciones;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Notifications.Infrastructure.Email;

public sealed class OpcionesEmail
{
    public const string Seccion = "Email";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Remitente { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
}

#pragma warning disable SYSLIB0014 // SmtpClient está obsoleto; se usa para el piloto con servidor configurable.
public sealed class SmtpEmailSender(IOptions<OpcionesEmail> opciones) : IEmailSender
{
    public async Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoTexto,
        string? cuerpoHtml = null,
        CancellationToken ct = default)
    {
        var opc = opciones.Value;
        using var cliente = new SmtpClient(opc.Host, opc.Port)
        {
            Credentials = new NetworkCredential(opc.Usuario, opc.Password),
            EnableSsl = opc.EnableSsl,
        };
        using var mensaje = new MailMessage(opc.Remitente, destinatario, asunto, cuerpoTexto)
        {
            IsBodyHtml = !string.IsNullOrEmpty(cuerpoHtml),
            Body = cuerpoHtml ?? cuerpoTexto,
        };
        await cliente.SendMailAsync(mensaje, ct);
    }
}
#pragma warning restore SYSLIB0014
