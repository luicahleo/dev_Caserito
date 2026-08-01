using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class SolicitarRestablecimientoPasswordHandlerTests
{
    private sealed class RepositorioFake(SolicitudRestablecimiento? solicitud)
        : IRepositorioRestablecimientoPassword
    {
        public Task<SolicitudRestablecimiento?> CrearSolicitudAsync(
            string email,
            CancellationToken cancellationToken) => Task.FromResult(solicitud);

        public Task<Result<Guid>> RestablecerAsync(
            Guid usuarioId,
            string token,
            string password,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ServicioCorreoFake(bool falla = false) : IServicioCorreo
    {
        public MensajeCorreo? Mensaje { get; private set; }

        public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            if (falla)
            {
                throw new InvalidOperationException("SMTP no disponible");
            }

            Mensaje = mensaje;
            return Task.CompletedTask;
        }
    }

    private sealed class PlantillaFake : IPlantillaCorreo
    {
        public string AsuntoConfirmacionEmail(string nombre) => string.Empty;
        public string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion) => string.Empty;
        public string AsuntoKycAprobado(string nombre) => string.Empty;
        public string CuerpoKycAprobado(string nombre) => string.Empty;
        public string AsuntoKycRechazado(string nombre) => string.Empty;
        public string CuerpoKycRechazado(string nombre, string motivo) => string.Empty;
        public string AsuntoRestablecimientoPassword(string nombre) => "Recuperación";
        public string CuerpoRestablecimientoPassword(string nombre, string urlRestablecimiento) =>
            urlRestablecimiento;
    }

    private sealed class LoggerCapturador<T> : ILogger<T>
    {
        public string Mensaje { get; private set; } = string.Empty;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Mensaje = formatter(state, exception);
    }

    [Fact]
    public async Task Cuenta_existente_envia_enlace_con_token_en_fragmento()
    {
        var usuarioId = Guid.NewGuid();
        var correo = new ServicioCorreoFake();
        var handler = CrearHandler(
            new SolicitudRestablecimiento(usuarioId, "ana@example.test", "Ana", "token +/="),
            correo);

        var resultado = await handler.Handle(
            new SolicitarRestablecimientoPasswordCommand("ana@example.test"),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.NotNull(correo.Mensaje);
        Assert.Equal("ana@example.test", correo.Mensaje.Para);
        Assert.Contains($"/restablecer-password#usuarioId={usuarioId}", correo.Mensaje.CuerpoTexto);
        Assert.Contains("&token=token%20%2B%2F%3D", correo.Mensaje.CuerpoTexto);
    }

    [Fact]
    public async Task Cuenta_inexistente_devuelve_exito_sin_enviar_correo()
    {
        var correo = new ServicioCorreoFake();
        var handler = CrearHandler(null, correo);

        var resultado = await handler.Handle(
            new SolicitarRestablecimientoPasswordCommand("nadie@example.test"),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Null(correo.Mensaje);
    }

    [Fact]
    public async Task Fallo_smtp_devuelve_exito_y_log_no_contiene_datos_sensibles()
    {
        var logger = new LoggerCapturador<SolicitarRestablecimientoPasswordCommandHandler>();
        var handler = CrearHandler(
            new SolicitudRestablecimiento(
                Guid.NewGuid(),
                "sensible@example.test",
                "Ana",
                "token-sensible"),
            new ServicioCorreoFake(falla: true),
            logger);

        var resultado = await handler.Handle(
            new SolicitarRestablecimientoPasswordCommand("sensible@example.test"),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.DoesNotContain("sensible@example.test", logger.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("token-sensible", logger.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("restablecer-password", logger.Mensaje, StringComparison.Ordinal);
    }

    private static SolicitarRestablecimientoPasswordCommandHandler CrearHandler(
        SolicitudRestablecimiento? solicitud,
        IServicioCorreo correo,
        ILogger<SolicitarRestablecimientoPasswordCommandHandler>? logger = null) =>
        new(
            new RepositorioFake(solicitud),
            correo,
            new PlantillaFake(),
            new OpcionesApp { UrlPublica = "https://app.ejemplo.test/" },
            logger ?? new LoggerCapturador<SolicitarRestablecimientoPasswordCommandHandler>());
}
