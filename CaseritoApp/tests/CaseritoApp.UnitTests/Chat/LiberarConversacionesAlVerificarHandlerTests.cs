using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Conversaciones;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class LiberarConversacionesAlVerificarHandlerTests
{
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();

    private sealed class RepoFake(List<Conversacion> retenidas) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }

        public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
            Guid compradorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Conversacion>>(
                compradorId == _comprador ? retenidas : []);
    }

    private sealed class MensajesFake : IRepositorioMensajes
    {
        public Task<Mensaje?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);

        public Task<Mensaje?> ObtenerPorClaveAsync(
            Guid conversacionId, Guid remitenteId, Guid clave, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);

        public Task<long> ReservarSecuenciaAsync(CancellationToken ct) => Task.FromResult(1L);

        public void Agregar(Mensaje mensaje)
        {
        }

        // Devuelve el último mensaje realmente creado en la conversación pedida.
        public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
            Guid conversacionId, CancellationToken ct) =>
            Task.FromResult(Ultimos.GetValueOrDefault(conversacionId));

        public Dictionary<Guid, Mensaje> Ultimos { get; } = [];
    }

    private sealed class PublisherFake : IPublisher
    {
        public List<object> Publicados { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Publicados.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Publicados.Add(notification!);
            return Task.CompletedTask;
        }
    }

    private static Conversacion Retenida(MensajesFake mensajes)
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        mensajes.Ultimos[conversacion.Id] = mensaje;
        return conversacion;
    }

    [Fact]
    public async Task Libera_todas_las_conversaciones_retenidas_del_comprador()
    {
        var mensajes = new MensajesFake();
        var primera = Retenida(mensajes);
        var segunda = Retenida(mensajes);
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([primera, segunda]),
            mensajes,
            new PublisherFake(),
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, _comprador), default);

        Assert.Equal(EstadoConversacion.Activa, primera.Estado);
        Assert.Equal(EstadoConversacion.Activa, segunda.Estado);
    }

    [Fact]
    public async Task Publica_una_sola_notificacion_por_conversacion()
    {
        var mensajes = new MensajesFake();
        var conversacion = Retenida(mensajes);
        // Dos mensajes acumulados: la notificación debe seguir siendo una sola.
        var segundo = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 2, "sigo aquí", DateTimeOffset.UtcNow).Valor;
        mensajes.Ultimos[conversacion.Id] = segundo;
        var publisher = new PublisherFake();
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([conversacion]),
            mensajes,
            publisher,
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, _comprador), default);

        var evento = Assert.Single(publisher.Publicados.OfType<ChatMessageSent>());
        Assert.Equal(conversacion.Id, evento.ConversacionId);
        Assert.Equal(segundo.Id, evento.MensajeId);
        Assert.Equal(_vendedor, evento.DestinatarioId);
    }

    [Fact]
    public async Task Un_usuario_sin_conversaciones_retenidas_no_falla()
    {
        var publisher = new PublisherFake();
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([]),
            new MensajesFake(),
            publisher,
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()), default);

        Assert.Empty(publisher.Publicados);
    }
}
