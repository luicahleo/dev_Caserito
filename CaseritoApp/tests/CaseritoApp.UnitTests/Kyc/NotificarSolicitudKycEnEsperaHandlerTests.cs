using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class NotificarSolicitudKycEnEsperaHandlerTests
{
    private sealed class OpcionesAvisosFake(string? email) : IOpcionesAvisosKyc
    {
        public string? EmailAvisos => email;
    }

    private readonly IServicioCorreo _servicio = Substitute.For<IServicioCorreo>();

    private NotificarSolicitudKycEnEsperaHandler CrearHandler(string? buzon) =>
        new(_servicio,
            new PlantillaCorreoTextoPlano(),
            new OpcionesAvisosFake(buzon),
            new OpcionesApp { UrlPublica = "https://caserito.app" },
            NullLogger<NotificarSolicitudKycEnEsperaHandler>.Instance);

    private static SolicitudKycEnEspera Evento() =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Con_buzon_configurado_envia_el_aviso()
    {
        var handler = CrearHandler("admin@caserito.test");

        await handler.Handle(Evento(), CancellationToken.None);

        await _servicio.Received(1).EnviarAsync(
            Arg.Is<MensajeCorreo>(m =>
                m.Para == "admin@caserito.test"
                && m.CuerpoTexto.Contains("https://caserito.app/admin/kyc")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_buzon_configurado_no_envia_nada()
    {
        var handler = CrearHandler("   ");

        await handler.Handle(Evento(), CancellationToken.None);

        await _servicio.DidNotReceive().EnviarAsync(
            Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fallo_del_relay_no_se_propaga()
    {
        _servicio.EnviarAsync(Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relay caido"));
        var handler = CrearHandler("admin@caserito.test");

        var excepcion = await Record.ExceptionAsync(
            () => handler.Handle(Evento(), CancellationToken.None));

        Assert.Null(excepcion);
    }

    [Fact]
    public async Task El_aviso_no_contiene_identificadores_ni_score()
    {
        MensajeCorreo? capturado = null;
        await _servicio.EnviarAsync(
            Arg.Do<MensajeCorreo>(m => capturado = m), Arg.Any<CancellationToken>());
        var handler = CrearHandler("admin@caserito.test");
        var evento = Evento();

        await handler.Handle(evento, CancellationToken.None);

        Assert.NotNull(capturado);
        var texto = capturado!.Asunto + capturado.CuerpoTexto;
        Assert.DoesNotContain(evento.UsuarioId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(evento.SolicitudId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", texto, StringComparison.OrdinalIgnoreCase);
    }
}
