using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class ConfirmarCierreOrdenCommandHandlerTests
{
    [Fact]
    public async Task Confirmar_delega_en_el_agregado_y_es_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearMarcadaVendida(compradorId, vendedorId);
        var handler = new ConfirmarCierreOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new ConfirmarCierreOrdenCommand(orden.Id, compradorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Completed, orden.Estado);
        orden.LimpiarEventos();

        Assert.True((await handler.Handle(
            new ConfirmarCierreOrdenCommand(orden.Id, compradorId),
            CancellationToken.None)).EsExito);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public async Task Confirmar_oculta_ausente_o_actor_incorrecto()
    {
        var compradorId = Guid.NewGuid();
        var orden = CrearMarcadaVendida(compradorId, Guid.NewGuid());

        var incorrecto = await new ConfirmarCierreOrdenCommandHandler(new RepositorioFake(orden))
            .Handle(
                new ConfirmarCierreOrdenCommand(orden.Id, Guid.NewGuid()),
                CancellationToken.None);
        var ausente = await new ConfirmarCierreOrdenCommandHandler(new RepositorioFake(null))
            .Handle(
                new ConfirmarCierreOrdenCommand(Guid.NewGuid(), compradorId),
                CancellationToken.None);

        Assert.Equal(ErroresOrden.NoEncontrada, incorrecto.Error.Code);
        Assert.Equal(ErroresOrden.NoEncontrada, ausente.Error.Code);
    }

    private static Orden CrearMarcadaVendida(Guid compradorId, Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), compradorId, vendedorId, 100m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        var orden = resultado.Valor;
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        return orden;
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
