using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class NotificarKycResueltoHandlerTests
{
    private readonly IServicioCorreo _servicio = Substitute.For<IServicioCorreo>();
    private readonly IConsultaVerificacionKyc _consulta = Substitute.For<IConsultaVerificacionKyc>();
    private readonly NotificarKycResueltoHandler _handler;

    public NotificarKycResueltoHandlerTests()
    {
        _handler = new NotificarKycResueltoHandler(
            _servicio,
            new PlantillaCorreoTextoPlano(),
            _consulta,
            NullLogger<NotificarKycResueltoHandler>.Instance);
    }

    [Fact]
    public async Task Aprobado_envia_correo_de_aprobacion()
    {
        _consulta.ObtenerUsuarioAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UsuarioKycDto("test@caserito.test", "Luis"));

        await _handler.Handle(
            new KycResuelto(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), EstadoKyc.Aprobada, null),
            CancellationToken.None);

        await _servicio.Received(1).EnviarAsync(
            Arg.Is<MensajeCorreo>(m => m.Para == "test@caserito.test" && m.Asunto.Contains("verificada")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rechazado_envia_correo_con_motivo()
    {
        _consulta.ObtenerUsuarioAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UsuarioKycDto("test@caserito.test", "Luis"));

        await _handler.Handle(
            new KycResuelto(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), EstadoKyc.Rechazada, "El rostro no coincide"),
            CancellationToken.None);

        await _servicio.Received(1).EnviarAsync(
            Arg.Is<MensajeCorreo>(m => m.CuerpoTexto.Contains("El rostro no coincide")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Usuario_no_encontrado_no_envia_correo()
    {
        _consulta.ObtenerUsuarioAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UsuarioKycDto?)null);

        await _handler.Handle(
            new KycResuelto(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), EstadoKyc.Aprobada, null),
            CancellationToken.None);

        await _servicio.DidNotReceive().EnviarAsync(Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fallo_smtp_no_propaga_excepcion()
    {
        _consulta.ObtenerUsuarioAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UsuarioKycDto("test@caserito.test", "Luis"));
        _servicio.EnviarAsync(Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relay no disponible"));

        var excepcion = await Record.ExceptionAsync(() => _handler.Handle(
            new KycResuelto(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), EstadoKyc.Aprobada, null),
            CancellationToken.None));

        Assert.Null(excepcion);
    }
}
