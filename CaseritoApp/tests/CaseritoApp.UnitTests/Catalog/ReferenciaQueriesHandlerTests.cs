using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Catalogo;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ReferenciaQueriesHandlerTests
{
    private sealed class ConsultaFake : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([new CategoriaDto(Guid.NewGuid(), "Electrónica")]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([new CiudadDto(Guid.NewGuid(), "Cochabamba")]);
    }

    [Fact]
    public async Task Categorias_devuelve_lista()
    {
        var handler = new ListarCategoriasQueryHandler(new ConsultaFake());
        var r = await handler.Handle(new ListarCategoriasQuery(), CancellationToken.None);
        Assert.Single(r);
    }

    [Fact]
    public async Task Ciudades_devuelve_lista()
    {
        var handler = new ListarCiudadesQueryHandler(new ConsultaFake());
        var r = await handler.Handle(new ListarCiudadesQuery(), CancellationToken.None);
        Assert.Single(r);
    }
}
