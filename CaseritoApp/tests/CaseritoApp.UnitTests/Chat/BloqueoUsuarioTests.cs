using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Seguridad;

namespace CaseritoApp.UnitTests.Chat;

public sealed class BloqueoUsuarioTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_conserva_la_direccion_del_bloqueo()
    {
        var bloqueadorId = Guid.NewGuid();
        var bloqueadoId = Guid.NewGuid();
        var ahora = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

        var resultado = BloqueoUsuario.Crear(bloqueadorId, bloqueadoId, ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(bloqueadorId, resultado.Valor.BloqueadorId);
        Assert.Equal(bloqueadoId, resultado.Valor.BloqueadoId);
        Assert.Equal(ahora, resultado.Valor.CreadoEn);
    }

    [Fact]
    public void Crear_rechaza_participantes_iguales()
    {
        var usuarioId = Guid.NewGuid();

        var resultado = BloqueoUsuario.Crear(usuarioId, usuarioId, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
    }

    [Fact]
    public async Task Bloquear_deriva_la_contraparte_y_es_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        var bloqueos = new BloqueosFake();
        var handler = new BloquearUsuarioCommandHandler(
            new ConversacionesFake(conversacion), bloqueos, new RelojFijo(_ahora));
        var comando = new BloquearUsuarioCommand(conversacion.Id, compradorId);

        var primero = await handler.Handle(comando, CancellationToken.None);
        var segundo = await handler.Handle(comando, CancellationToken.None);

        Assert.True(primero.EsExito);
        Assert.True(segundo.EsExito);
        var bloqueo = Assert.Single(bloqueos.Agregados);
        Assert.Equal(compradorId, bloqueo.BloqueadorId);
        Assert.Equal(vendedorId, bloqueo.BloqueadoId);
    }

    [Fact]
    public async Task Desbloquear_quita_solo_el_bloqueo_del_actor_y_es_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        var bloqueos = new BloqueosFake();
        bloqueos.Agregar(BloqueoUsuario.Crear(compradorId, vendedorId, _ahora).Valor);
        bloqueos.Agregar(BloqueoUsuario.Crear(vendedorId, compradorId, _ahora).Valor);
        var handler = new DesbloquearUsuarioCommandHandler(
            new ConversacionesFake(conversacion), bloqueos);
        var comando = new DesbloquearUsuarioCommand(conversacion.Id, compradorId);

        var primero = await handler.Handle(comando, CancellationToken.None);
        var segundo = await handler.Handle(comando, CancellationToken.None);

        Assert.True(primero.EsExito);
        Assert.True(segundo.EsExito);
        var restante = Assert.Single(bloqueos.Agregados);
        Assert.Equal(vendedorId, restante.BloqueadorId);
        Assert.Equal(compradorId, restante.BloqueadoId);
    }

    private sealed class ConversacionesFake(Conversacion conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(id == conversacion.Id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    private sealed class BloqueosFake : IRepositorioBloqueosUsuario
    {
        public List<BloqueoUsuario> Agregados { get; } = [];

        public Task<bool> ExisteEntreAsync(Guid usuarioA, Guid usuarioB, CancellationToken ct) =>
            Task.FromResult(Agregados.Any(b =>
                (b.BloqueadorId == usuarioA && b.BloqueadoId == usuarioB)
                || (b.BloqueadorId == usuarioB && b.BloqueadoId == usuarioA)));

        public Task<BloqueoUsuario?> ObtenerAsync(
            Guid bloqueadorId, Guid bloqueadoId, CancellationToken ct) =>
            Task.FromResult(Agregados.SingleOrDefault(b =>
                b.BloqueadorId == bloqueadorId && b.BloqueadoId == bloqueadoId));

        public void Agregar(BloqueoUsuario bloqueo) => Agregados.Add(bloqueo);

        public void Quitar(BloqueoUsuario bloqueo) => Agregados.Remove(bloqueo);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
