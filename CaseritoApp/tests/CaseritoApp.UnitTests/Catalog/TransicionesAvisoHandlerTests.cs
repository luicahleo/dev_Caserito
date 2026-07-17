using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class TransicionesAvisoHandlerTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso aviso) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    [Fact]
    public async Task Pausar_ok_cuando_dueno_y_activo()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Pausado, aviso.Estado);
    }

    [Fact]
    public async Task Pausar_403_si_no_dueno()
    {
        var aviso = AvisoDe(Guid.NewGuid());
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEsPropietario, r.Error.Code);
    }

    [Fact]
    public async Task Pausar_404_si_no_existe()
    {
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(null), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }

    [Fact]
    public async Task Reactivar_ok_cuando_pausado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Pausar(_ahora);
        var handler = new ReactivarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new ReactivarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
    }

    [Fact]
    public async Task Reactivar_409_si_ya_activo()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new ReactivarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new ReactivarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.TransicionInvalida, r.Error.Code);
    }

    [Fact]
    public async Task Eliminar_ok_cuando_dueno()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new EliminarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new EliminarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Eliminado, aviso.Estado);
    }

    [Fact]
    public async Task Eliminar_404_si_ya_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);
        var handler = new EliminarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new EliminarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }
}
