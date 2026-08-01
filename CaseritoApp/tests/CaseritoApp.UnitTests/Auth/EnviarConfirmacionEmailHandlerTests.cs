using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class EnviarConfirmacionEmailHandlerTests
{
    private sealed class LoggerCapturador<T> : ILogger<T>
    {
        public Exception? Excepcion { get; private set; }
        public string Mensaje { get; private set; } = string.Empty;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Excepcion = exception;
            Mensaje = formatter(state, exception);
        }
    }

    private sealed class ServicioCorreoFake : IServicioCorreo
    {
        public MensajeCorreo? UltimoMensaje { get; private set; }

        public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            UltimoMensaje = mensaje;
            return Task.CompletedTask;
        }
    }

    private sealed class PlantillaFake : IPlantillaCorreo
    {
        public string AsuntoConfirmacionEmail(string nombre) => "Asunto";

        public string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion) => urlConfirmacion;

        public string AsuntoKycAprobado(string nombre) => "";

        public string CuerpoKycAprobado(string nombre) => "";

        public string AsuntoKycRechazado(string nombre) => "";

        public string CuerpoKycRechazado(string nombre, string motivo) => "";

        public string AsuntoRestablecimientoPassword(string nombre) => "";

        public string CuerpoRestablecimientoPassword(string nombre, string urlRestablecimiento) => "";
    }

    private sealed class GeneradorTokenFake : IGeneradorTokenEmail
    {
        public string Generar(Guid usuarioId) => "token-fake";

        public bool Validar(string token, out Guid usuarioId)
        {
            usuarioId = Guid.Empty;
            return false;
        }
    }

    private sealed class ServicioCorreoQueFalla : IServicioCorreo
    {
        public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
            => throw new InvalidOperationException("SMTP no disponible");
    }

    [Fact]
    public async Task Handle_envia_correo_de_confirmacion_con_url()
    {
        var servicio = new ServicioCorreoFake();
        var handler = new EnviarConfirmacionEmailHandler(
            servicio,
            new PlantillaFake(),
            new GeneradorTokenFake(),
            new OpcionesApp { UrlPublica = "https://app.ejemplo.test/" },
            NullLogger<EnviarConfirmacionEmailHandler>.Instance);

        var evento = new UsuarioRegistrado(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "test@test.com",
            "Luis");

        await handler.Handle(evento, CancellationToken.None);

        Assert.NotNull(servicio.UltimoMensaje);
        Assert.Contains(
            "https://app.ejemplo.test/confirmar-email?userId=", servicio.UltimoMensaje.CuerpoTexto);
        Assert.Contains("token-fake", servicio.UltimoMensaje.CuerpoTexto);
        Assert.Equal("test@test.com", servicio.UltimoMensaje.Para);
    }

    [Fact]
    public async Task Handle_no_propaga_excepcion_cuando_falla_el_envio()
    {
        var logger = new LoggerCapturador<EnviarConfirmacionEmailHandler>();
        var handler = new EnviarConfirmacionEmailHandler(
            new ServicioCorreoQueFalla(),
            new PlantillaFake(),
            new GeneradorTokenFake(),
            new OpcionesApp { UrlPublica = "https://app.ejemplo.test/" },
            logger);

        var evento = new UsuarioRegistrado(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "test@test.com",
            "Luis");

        var excepcion = await Record.ExceptionAsync(
            () => handler.Handle(evento, CancellationToken.None));

        Assert.Null(excepcion);
        Assert.IsType<InvalidOperationException>(logger.Excepcion);
        Assert.DoesNotContain("token-fake", logger.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("test@test.com", logger.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", logger.Mensaje, StringComparison.Ordinal);
    }
}
