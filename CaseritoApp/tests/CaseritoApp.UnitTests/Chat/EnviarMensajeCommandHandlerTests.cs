using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class EnviarMensajeCommandHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class ConversacionesFake(Conversacion? conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(conversacion?.Id == id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    private sealed class MensajesFake : IRepositorioMensajes
    {
        public Mensaje? Existente { get; set; }

        public int Reservas { get; private set; }

        public List<Mensaje> Agregados { get; } = [];

        public Task<Mensaje?> ObtenerPorClaveAsync(
            Guid conversacionId, Guid remitenteId, Guid clave, CancellationToken ct) =>
            Task.FromResult(Existente);

        public Task<long> ReservarSecuenciaAsync(CancellationToken ct)
        {
            Reservas++;
            return Task.FromResult(10L);
        }

        public void Agregar(Mensaje mensaje) => Agregados.Add(mensaje);
    }

    [Fact]
    public async Task Nuevo_mensaje_reserva_secuencia_y_agrega()
    {
        var remitenteId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), remitenteId, Guid.NewGuid(), _ahora).Valor;
        var mensajes = new MensajesFake();
        var handler = new EnviarMensajeCommandHandler(
            new ConversacionesFake(conversacion), mensajes, new RelojFijo(_ahora.AddMinutes(1)));

        var resultado = await handler.Handle(
            new EnviarMensajeCommand(conversacion.Id, remitenteId, Guid.NewGuid(), " Hola "),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.True(resultado.Valor.FueCreado);
        Assert.Equal(10, resultado.Valor.Mensaje.Secuencia);
        Assert.Equal("Hola", resultado.Valor.Mensaje.Texto);
        Assert.Equal(1, mensajes.Reservas);
        Assert.Single(mensajes.Agregados);
    }

    [Fact]
    public async Task Reintento_identico_devuelve_original_sin_reservar_ni_agregar()
    {
        var remitenteId = Guid.NewGuid();
        var clave = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), remitenteId, Guid.NewGuid(), _ahora).Valor;
        var existente = conversacion.CrearMensaje(
            remitenteId, clave, 4, "Hola", _ahora.AddMinutes(1)).Valor;
        var mensajes = new MensajesFake { Existente = existente };
        var handler = new EnviarMensajeCommandHandler(
            new ConversacionesFake(conversacion), mensajes, TimeProvider.System);

        var resultado = await handler.Handle(
            new EnviarMensajeCommand(conversacion.Id, remitenteId, clave, "  Hola  "),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.False(resultado.Valor.FueCreado);
        Assert.Equal(existente.Id, resultado.Valor.Mensaje.Id);
        Assert.Equal(0, mensajes.Reservas);
        Assert.Empty(mensajes.Agregados);
    }

    [Fact]
    public async Task Misma_clave_con_texto_distinto_devuelve_conflicto()
    {
        var remitenteId = Guid.NewGuid();
        var clave = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), remitenteId, Guid.NewGuid(), _ahora).Valor;
        var existente = conversacion.CrearMensaje(
            remitenteId, clave, 4, "Hola", _ahora.AddMinutes(1)).Valor;
        var mensajes = new MensajesFake { Existente = existente };
        var handler = new EnviarMensajeCommandHandler(
            new ConversacionesFake(conversacion), mensajes, TimeProvider.System);

        var resultado = await handler.Handle(
            new EnviarMensajeCommand(conversacion.Id, remitenteId, clave, "Otro"),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.ClaveIdempotenciaReutilizada, resultado.Error.Code);
        Assert.Equal(0, mensajes.Reservas);
    }

    [Fact]
    public async Task Conversacion_ausente_o_tercero_devuelven_el_mismo_error()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var comando = new EnviarMensajeCommand(
            conversacion.Id, Guid.NewGuid(), Guid.NewGuid(), "Hola");

        var ausente = await new EnviarMensajeCommandHandler(
            new ConversacionesFake(null), new MensajesFake(), TimeProvider.System)
            .Handle(comando, CancellationToken.None);
        var tercero = await new EnviarMensajeCommandHandler(
            new ConversacionesFake(conversacion), new MensajesFake(), TimeProvider.System)
            .Handle(comando, CancellationToken.None);

        Assert.Equal(ErroresConversacion.NoEncontrada, ausente.Error.Code);
        Assert.Equal(ausente.Error.Code, tercero.Error.Code);
    }

    [Fact]
    public void Validator_rechaza_campos_invalidos_sin_ecos_del_texto()
    {
        const string contenido = "contenido-muy-sensible";
        var validator = new EnviarMensajeCommandValidator();

        var resultado = validator.Validate(new EnviarMensajeCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, contenido + new string('a', 2001)));

        Assert.False(resultado.IsValid);
        Assert.DoesNotContain(resultado.Errors, e => e.ErrorMessage.Contains(contenido, StringComparison.Ordinal));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
