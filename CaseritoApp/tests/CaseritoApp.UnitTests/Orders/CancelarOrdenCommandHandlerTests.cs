using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class CancelarOrdenCommandHandlerTests
{
    [Fact]
    public async Task Cancelar_por_participante_cambia_estado_y_emite_evento()
    {
        var compradorId = Guid.NewGuid();
        var orden = CrearOrden(compradorId, Guid.NewGuid());
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(orden.Id, compradorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
        Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
    }

    [Fact]
    public async Task Cancelar_oculta_una_orden_ausente()
    {
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(null));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public async Task Cancelar_oculta_la_orden_a_un_tercero()
    {
        var orden = CrearOrden(Guid.NewGuid(), Guid.NewGuid());
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(orden.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Empty(orden.EventosDeDominio);
    }

    private static Orden CrearOrden(Guid compradorId, Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), compradorId, vendedorId, 100m, "BOB", DateTimeOffset.UtcNow);
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

        public void Agregar(Orden orden)
        {
        }
    }
}
