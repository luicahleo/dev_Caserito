using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class AceptarOrdenCommandHandlerTests
{
    [Fact]
    public async Task Aceptar_cambia_la_orden_solicitada_a_acordada()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(vendedorId);
        var handler = new AceptarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new AceptarOrdenCommand(orden.Id, vendedorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Agreed, orden.Estado);
        Assert.Single(orden.EventosDeDominio);
        Assert.IsType<EstadoOrdenCambiado>(orden.EventosDeDominio.Single());
    }

    [Fact]
    public async Task Aceptar_oculta_una_orden_ausente()
    {
        var handler = new AceptarOrdenCommandHandler(new RepositorioFake(null));

        var resultado = await handler.Handle(
            new AceptarOrdenCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public async Task Aceptar_oculta_la_orden_a_un_tercero()
    {
        var orden = CrearOrden(Guid.NewGuid());
        var handler = new AceptarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new AceptarOrdenCommand(orden.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Equal(EstadoOrden.Requested, orden.Estado);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public async Task Aceptar_reintento_es_idempotente_y_no_agrega_evento()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var handler = new AceptarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new AceptarOrdenCommand(orden.Id, vendedorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Empty(orden.EventosDeDominio);
    }

    private static Orden CrearOrden(Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            vendedorId,
            100m,
            "BOB",
            DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        resultado.Valor.LimpiarEventos();
        return resultado.Valor;
    }

    private sealed class RepositorioFake(Orden? orden) : IRepositorioOrdenes
    {
        public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<Orden?> ObtenerAsync(Guid ordenId, CancellationToken ct) =>
            Task.FromResult(orden);

        public Task<IReadOnlyList<Orden>> ObtenerAbiertasPorAvisoAsync(
            Guid avisoId,
            Guid excluirOrdenId,
            CancellationToken ct)
        {
            _ = (avisoId, excluirOrdenId, ct);
            return Task.FromResult<IReadOnlyList<Orden>>([]);
        }

        public void Agregar(Orden orden)
        {
        }
    }
}
