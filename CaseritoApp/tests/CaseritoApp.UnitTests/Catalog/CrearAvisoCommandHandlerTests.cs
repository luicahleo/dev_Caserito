using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class CrearAvisoCommandHandlerTests
{
    private static readonly Guid _categoria = Guid.NewGuid();
    private static readonly Guid _ciudad = Guid.NewGuid();

    private sealed class RepositorioFake : IRepositorioAvisos
    {
        public Aviso? Agregado { get; private set; }

        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<Aviso?>(null);

        public void Agregar(Aviso aviso) => Agregado = aviso;

        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private sealed class ConsultaFake(bool categoria, bool ciudad) : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct) => Task.FromResult(categoria);
        public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct) => Task.FromResult(ciudad);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([]);
    }

    private static CrearAvisoCommand Comando(bool verificado = true) => new(
        VendedorId: Guid.NewGuid(),
        EstaVerificado: verificado,
        Titulo: "Bicicleta",
        Descripcion: "Poco uso",
        Monto: 500m,
        Condicion: "Usado",
        CategoriaId: _categoria,
        CiudadId: _ciudad);

    private static CrearAvisoCommandHandler Handler(
        RepositorioFake repo, bool categoria = true, bool ciudad = true) =>
        new(repo, new ConsultaFake(categoria, ciudad), TimeProvider.System);

    [Fact]
    public async Task Crea_aviso_cuando_verificado_y_catalogo_valido()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo).Handle(Comando(), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.NotNull(repo.Agregado);
        Assert.Equal(resultado.Valor, repo.Agregado!.Id);
        Assert.Equal(EstadoAviso.Activo, repo.Agregado.Estado);
    }

    [Fact]
    public async Task Falla_con_no_verificado_si_usuario_no_esta_verificado()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo).Handle(Comando(verificado: false), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoVerificado, resultado.Error.Code);
        Assert.Null(repo.Agregado);
    }

    [Fact]
    public async Task Falla_con_categoria_invalida()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo, categoria: false).Handle(Comando(), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.CategoriaInvalida, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_con_ciudad_invalida()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo, ciudad: false).Handle(Comando(), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.CiudadInvalida, resultado.Error.Code);
    }
}
