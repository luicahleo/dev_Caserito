using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class MarcarOrdenVendidaCommandHandlerTests
{
    [Fact]
    public async Task Marcar_vendida_cierra_ganadora_y_cancela_competidoras_abiertas()
    {
        var avisoId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var ganadora = CrearOrden(avisoId, Guid.NewGuid(), vendedorId, acordada: true);
        var solicitada = CrearOrden(avisoId, Guid.NewGuid(), vendedorId);
        var acordada = CrearOrden(avisoId, Guid.NewGuid(), vendedorId, acordada: true);
        var repositorio = new RepositorioFake(ganadora, [solicitada, acordada]);
        var handler = new MarcarOrdenVendidaCommandHandler(repositorio);

        var resultado = await handler.Handle(
            new MarcarOrdenVendidaCommand(ganadora.Id, vendedorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(avisoId, resultado.Valor.AvisoId);
        Assert.Equal(EstadoOrden.MarkedAsSold, ganadora.Estado);
        Assert.Equal(EstadoOrden.Cancelled, solicitada.Estado);
        Assert.Equal(EstadoOrden.Cancelled, acordada.Estado);
        Assert.All([solicitada, acordada], orden =>
            Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio)));
    }

    [Fact]
    public async Task Marcar_vendida_oculta_ausente_y_actor_incorrecto_sin_consultar_competidoras()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(Guid.NewGuid(), Guid.NewGuid(), vendedorId, acordada: true);
        var repositorio = new RepositorioFake(orden, []);
        var handler = new MarcarOrdenVendidaCommandHandler(repositorio);

        var incorrecto = await handler.Handle(
            new MarcarOrdenVendidaCommand(orden.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(incorrecto.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, incorrecto.Error.Code);
        Assert.False(repositorio.ConsultoCompetidoras);

        var ausente = await new MarcarOrdenVendidaCommandHandler(new RepositorioFake(null, []))
            .Handle(
                new MarcarOrdenVendidaCommand(Guid.NewGuid(), vendedorId),
                CancellationToken.None);
        Assert.Equal(ErroresOrden.NoEncontrada, ausente.Error.Code);
    }

    [Fact]
    public async Task Reintento_no_vuelve_a_consultar_ni_mutar_competidoras()
    {
        var vendedorId = Guid.NewGuid();
        var ganadora = CrearOrden(Guid.NewGuid(), Guid.NewGuid(), vendedorId, acordada: true);
        Assert.True(ganadora.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        ganadora.LimpiarEventos();
        var repositorio = new RepositorioFake(ganadora, []);

        var resultado = await new MarcarOrdenVendidaCommandHandler(repositorio).Handle(
            new MarcarOrdenVendidaCommand(ganadora.Id, vendedorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.False(repositorio.ConsultoCompetidoras);
        Assert.Empty(ganadora.EventosDeDominio);
    }

    private static Orden CrearOrden(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        bool acordada = false)
    {
        var resultado = Orden.Crear(
            avisoId, compradorId, vendedorId, 100m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        var orden = resultado.Valor;
        if (acordada)
        {
            Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        }

        orden.LimpiarEventos();
        return orden;
    }

    private sealed class RepositorioFake(
        Orden? orden,
        IReadOnlyList<Orden> competidoras) : IRepositorioOrdenes
    {
        public bool ConsultoCompetidoras { get; private set; }

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
            ConsultoCompetidoras = true;
            return Task.FromResult(competidoras);
        }

        public void Agregar(Orden orden)
        {
        }
    }
}
