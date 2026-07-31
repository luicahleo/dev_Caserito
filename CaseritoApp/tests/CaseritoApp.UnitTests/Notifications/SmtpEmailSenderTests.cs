using CaseritoApp.Notifications.Infrastructure.Email;
using CaseritoApp.UnitTests.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task Enviar_sin_ssl_y_usuario_vacio_no_inicia_tls_ni_autenticacion()
    {
        await using var servidor = new ServidorSmtpPrueba(anunciarStartTls: true);
        var sender = new SmtpEmailSender(
            Options.Create(new OpcionesEmail
            {
                Host = "127.0.0.1",
                Port = servidor.Puerto,
                Remitente = "noreply@example.test",
                Usuario = "",
                Password = "secreto-que-no-debe-usarse",
                EnableSsl = false,
            }),
            NullLogger<SmtpEmailSender>.Instance);

        await sender.EnviarAsync(
            "destino@example.test",
            "Prueba",
            "Contenido sin datos sensibles",
            ct: CancellationToken.None);

        Assert.DoesNotContain(servidor.Comandos, comando => comando.StartsWith("STARTTLS", StringComparison.Ordinal));
        Assert.DoesNotContain(servidor.Comandos, comando => comando.StartsWith("AUTH", StringComparison.Ordinal));
        Assert.Contains(servidor.Comandos, comando => comando.StartsWith("MAIL", StringComparison.Ordinal));
    }
}
