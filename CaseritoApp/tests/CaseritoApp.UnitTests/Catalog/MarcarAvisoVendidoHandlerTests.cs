using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class MarcarAvisoVendidoHandlerTests
{
    [Fact]
    public async Task Preflight_y_command_ocultan_ausente_o_dueño_incorrecto()
    {
        var aviso = CrearAviso();
        var repositorio = new RepositorioFake(aviso);

        var preflight = await new ValidarAvisoParaVentaQueryHandler(repositorio).Handle(
            new ValidarAvisoParaVentaQuery(aviso.Id, Guid.NewGuid()),
            CancellationToken.None);
        var command = await new MarcarAvisoVendidoCommandHandler(
            repositorio, TimeProvider.System).Handle(
                new MarcarAvisoVendidoCommand(aviso.Id, Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, preflight.Error.Code);
        Assert.Equal(ErroresAviso.NoEncontrado, command.Error.Code);
    }

    [Fact]
    public async Task Command_marca_vendido_y_reintento_de_misma_orden_converge()
    {
        var aviso = CrearAviso();
        var ordenId = Guid.NewGuid();
        var handler = new MarcarAvisoVendidoCommandHandler(
            new RepositorioFake(aviso), TimeProvider.System);
        var command = new MarcarAvisoVendidoCommand(
            aviso.Id, ordenId, aviso.VendedorId);

        Assert.True((await handler.Handle(command, CancellationToken.None)).EsExito);
        aviso.LimpiarEventos();
        Assert.True((await handler.Handle(command, CancellationToken.None)).EsExito);

        Assert.Equal(EstadoAviso.Vendido, aviso.Estado);
        Assert.Equal(ordenId, aviso.OrdenVentaId);
        Assert.Empty(aviso.EventosDeDominio);
    }

    private static Aviso CrearAviso() => Aviso.Crear(
        Guid.NewGuid(),
        "Artículo",
        "Descripción",
        Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(),
        Guid.NewGuid(),
        CondicionArticulo.Usado,
        DateTime.UtcNow);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct)
        {
            _ = (id, ct);
            return Task.FromResult(aviso);
        }

        public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct) =>
            ObtenerAsync(id, ct);

        public void Agregar(Aviso aviso) => _ = aviso;

        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId,
            int pagina,
            int tamano,
            CancellationToken ct)
        {
            _ = (vendedorId, ct);
            return Task.FromResult(
                new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
        }
    }
}
