using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class BuscarAvisosQueryHandlerTests
{
    private sealed class ConsultaFake : IConsultaAvisosPublica
    {
        public FiltroBusquedaAvisos? UltimoFiltro { get; private set; }
        public int UltimaPagina { get; private set; }
        public int UltimoTamano { get; private set; }

        public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
            FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            UltimaPagina = pagina;
            UltimoTamano = tamano;
            return Task.FromResult(new ResultadoPaginado<AvisoPublicoResumenDto>([], pagina, tamano, 0));
        }

        public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<AvisoPublicoDto?>(null);
    }

    [Fact]
    public async Task Tokeniza_el_texto_y_parsea_la_condicion()
    {
        var fake = new ConsultaFake();
        var handler = new BuscarAvisosQueryHandler(fake);

        await handler.Handle(
            new BuscarAvisosQuery("  bici  montaña ", null, null, null, null, "usado", 2, 10),
            CancellationToken.None);

        Assert.NotNull(fake.UltimoFiltro);
        Assert.Equal(["bici", "montaña"], fake.UltimoFiltro!.Tokens);
        Assert.Equal(CondicionArticulo.Usado, fake.UltimoFiltro.Condicion);
        Assert.Equal(2, fake.UltimaPagina);
        Assert.Equal(10, fake.UltimoTamano);
    }

    [Fact]
    public async Task Texto_vacio_produce_cero_tokens()
    {
        var fake = new ConsultaFake();
        var handler = new BuscarAvisosQueryHandler(fake);

        await handler.Handle(
            new BuscarAvisosQuery("   ", null, null, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Empty(fake.UltimoFiltro!.Tokens);
        Assert.Null(fake.UltimoFiltro.Condicion);
    }
}
