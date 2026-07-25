using CaseritoApp.Catalog.Application.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ObtenerAvisoPublicoQueryHandlerTests
{
    private sealed class ConsultaFake(AvisoPublicoDto? dto) : IConsultaAvisosPublica
    {
        public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
            FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoPublicoResumenDto>([], pagina, tamano, 0));

        public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(dto);

        public Task<ReferenciaAvisoContactableDto?> ObtenerReferenciaContactableAsync(
            Guid id,
            CancellationToken ct) =>
            Task.FromResult<ReferenciaAvisoContactableDto?>(null);
    }

    [Fact]
    public async Task Devuelve_el_dto_cuando_existe()
    {
        var esperado = new AvisoPublicoDto(
            Guid.NewGuid(), Guid.NewGuid(), "Bici", "desc", 100m, "BOB", "Deportes", "La Paz", "Usado", DateTime.UtcNow, []);
        var handler = new ObtenerAvisoPublicoQueryHandler(new ConsultaFake(esperado));

        var dto = await handler.Handle(new ObtenerAvisoPublicoQuery(esperado.Id), CancellationToken.None);

        Assert.Equal(esperado, dto);
    }

    [Fact]
    public async Task Devuelve_null_cuando_no_existe()
    {
        var handler = new ObtenerAvisoPublicoQueryHandler(new ConsultaFake(null));

        var dto = await handler.Handle(new ObtenerAvisoPublicoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(dto);
    }
}
