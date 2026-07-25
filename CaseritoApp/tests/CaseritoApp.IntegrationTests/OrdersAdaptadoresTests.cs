using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Host.Orders;
using CaseritoApp.Identity.Application.Kyc;

namespace CaseritoApp.IntegrationTests;

public sealed class OrdersAdaptadoresTests
{
    [Fact]
    public async Task Catalog_entrega_la_instantanea_comercial_al_puerto_de_orders()
    {
        var referencia = new ReferenciaAvisoContactableDto(
            Guid.NewGuid(), Guid.NewGuid(), "Artículo sintético", 45.60m, "BOB");
        var adapter = new ConsultaAvisoParaOrdenAdapter(new ConsultaAvisosFake(referencia));

        var resultado = await adapter.ObtenerAsync(referencia.AvisoId, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(referencia.VendedorId, resultado.VendedorId);
        Assert.Equal(referencia.Monto, resultado.Monto);
        Assert.Equal(referencia.Moneda, resultado.Moneda);
    }

    [Fact]
    public async Task Identity_solo_entrega_el_booleano_de_verificacion()
    {
        var adapter = new ConsultaVerificacionParticipanteAdapter(
            new ConsultaVerificacionFake(true));

        var resultado = await adapter.EstaVerificadoAsync(
            Guid.NewGuid(), CancellationToken.None);

        Assert.True(resultado);
    }

    private sealed class ConsultaAvisosFake(ReferenciaAvisoContactableDto referencia)
        : IConsultaAvisosPublica
    {
        public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
            FiltroBusquedaAvisos filtro,
            int pagina,
            int tamano,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ReferenciaAvisoContactableDto?> ObtenerReferenciaContactableAsync(
            Guid id,
            CancellationToken ct) =>
            Task.FromResult<ReferenciaAvisoContactableDto?>(referencia);
    }

    private sealed class ConsultaVerificacionFake(bool verificado) : IConsultaVerificacionKyc
    {
        public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(verificado);
    }
}
