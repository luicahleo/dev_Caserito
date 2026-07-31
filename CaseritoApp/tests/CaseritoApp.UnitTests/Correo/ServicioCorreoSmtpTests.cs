using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class ServicioCorreoSmtpTests
{
    [Fact]
    public void Construye_con_opciones_por_defecto()
    {
        var opciones = Options.Create(new OpcionesCorreo());
        var servicio = new ServicioCorreoSmtp(opciones, NullLogger<ServicioCorreoSmtp>.Instance);
        Assert.NotNull(servicio);
    }

    [Fact]
    public async Task Enviar_sin_ssl_no_intenta_starttls()
    {
        await using var servidor = new ServidorSmtpPrueba(anunciarStartTls: true);
        var servicio = CrearServicio(servidor.Puerto, habilitarSsl: false);

        await servicio.EnviarAsync(CrearMensaje(), CancellationToken.None);

        Assert.DoesNotContain(servidor.Comandos, comando => comando.StartsWith("STARTTLS", StringComparison.Ordinal));
        Assert.Contains(servidor.Comandos, comando => comando.StartsWith("MAIL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enviar_con_ssl_selecciona_starttls()
    {
        await using var servidor = new ServidorSmtpPrueba(anunciarStartTls: true);
        var servicio = CrearServicio(servidor.Puerto, habilitarSsl: true);

        await Assert.ThrowsAnyAsync<Exception>(
            () => servicio.EnviarAsync(CrearMensaje(), CancellationToken.None));

        Assert.Contains(servidor.Comandos, comando => comando.StartsWith("STARTTLS", StringComparison.Ordinal));
    }

    private static ServicioCorreoSmtp CrearServicio(int puerto, bool habilitarSsl)
    {
        var opciones = Options.Create(new OpcionesCorreo
        {
            Host = "127.0.0.1",
            Puerto = puerto,
            HabilitarSsl = habilitarSsl,
        });
        return new ServicioCorreoSmtp(opciones, NullLogger<ServicioCorreoSmtp>.Instance);
    }

    private static MensajeCorreo CrearMensaje()
        => new("destino@example.test", "Prueba", "Contenido sin datos sensibles");
}
