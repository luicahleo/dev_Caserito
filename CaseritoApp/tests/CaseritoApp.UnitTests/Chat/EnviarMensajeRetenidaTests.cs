using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Seguridad;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class EnviarMensajeRetenidaTests
{
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();

    private sealed class ConversacionesFake(Conversacion conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(id == conversacion.Id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }

        public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
            Guid compradorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Conversacion>>([]);
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

        public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
            Guid conversacionId, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);
    }

    private sealed class BloqueosFake : IRepositorioBloqueosUsuario
    {
        public Task<bool> ExisteEntreAsync(Guid usuarioA, Guid usuarioB, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<BloqueoUsuario?> ObtenerAsync(
            Guid bloqueadorId, Guid bloqueadoId, CancellationToken ct) =>
            Task.FromResult<BloqueoUsuario?>(null);

        public void Agregar(BloqueoUsuario bloqueo)
        {
        }

        public void Quitar(BloqueoUsuario bloqueo)
        {
        }
    }

    private static EnviarMensajeCommandHandler CrearHandler(Conversacion conversacion) =>
        new(new ConversacionesFake(conversacion), new MensajesFake(),
            TimeProvider.System, new BloqueosFake());

    [Fact]
    public async Task Mensaje_en_conversacion_retenida_marca_el_resultado_como_retenido()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;

        var resultado = await CrearHandler(conversacion).Handle(
            new EnviarMensajeCommand(conversacion.Id, _comprador, Guid.NewGuid(), "hola"),
            default);

        Assert.True(resultado.EsExito);
        Assert.True(resultado.Valor.Retenida);
    }

    [Fact]
    public async Task Mensaje_en_conversacion_activa_no_marca_retenido()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow).Valor;

        var resultado = await CrearHandler(conversacion).Handle(
            new EnviarMensajeCommand(conversacion.Id, _comprador, Guid.NewGuid(), "hola"),
            default);

        Assert.True(resultado.EsExito);
        Assert.False(resultado.Valor.Retenida);
    }
}
