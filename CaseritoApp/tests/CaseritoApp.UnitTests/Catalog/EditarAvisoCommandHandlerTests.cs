using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class EditarAvisoCommandHandlerTests
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

    private sealed class ConsultaFake : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([]);
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    private static EditarAvisoCommand Comando(Guid id, Guid vendedor) => new(
        id, vendedor, "Nuevo", "Nueva desc", 20m, "Usado", Guid.NewGuid(), Guid.NewGuid());

    private static EditarAvisoCommandHandler Handler(IRepositorioAvisos repo) =>
        new(repo, new ConsultaFake(), TimeProvider.System);

    [Fact]
    public async Task Edita_cuando_es_propietario()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal("Nuevo", aviso.Titulo);
    }

    [Fact]
    public async Task Falla_404_si_no_existe()
    {
        var resultado = await Handler(new RepositorioFake(null)).Handle(
            Comando(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_404_si_esta_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, vendedor), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_403_si_no_es_propietario()
    {
        var aviso = AvisoDe(Guid.NewGuid());

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEsPropietario, resultado.Error.Code);
    }
}
