using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class SolicitarOrdenCommandHandlerTests
{
    [Fact]
    public async Task Solicitar_usa_la_instantanea_del_aviso()
    {
        var aviso = new AvisoParaOrden(
            Guid.NewGuid(), Guid.NewGuid(), "Artículo sintético", 321.45m, "BOB");
        var repositorio = new RepositorioFake();
        var handler = new SolicitarOrdenCommandHandler(
            repositorio,
            new ConsultaAvisoFake(aviso),
            new VerificacionFake(true));
        var compradorId = Guid.NewGuid();

        var resultado = await handler.Handle(
            new SolicitarOrdenCommand(aviso.AvisoId, compradorId, true),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.NotNull(repositorio.Agregada);
        Assert.Equal(321.45m, repositorio.Agregada.MontoAcordado);
        Assert.Equal("BOB", repositorio.Agregada.Moneda);
    }

    [Fact]
    public async Task Solicitar_rechaza_participante_no_verificado()
    {
        var aviso = new AvisoParaOrden(
            Guid.NewGuid(), Guid.NewGuid(), "Artículo sintético", 10m, "BOB");
        var repositorio = new RepositorioFake();
        var handler = new SolicitarOrdenCommandHandler(
            repositorio,
            new ConsultaAvisoFake(aviso),
            new VerificacionFake(false));

        var resultado = await handler.Handle(
            new SolicitarOrdenCommand(aviso.AvisoId, Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoVerificado, resultado.Error.Code);
        Assert.Null(repositorio.Agregada);
    }

    [Fact]
    public async Task Solicitar_rechaza_duplicado()
    {
        var aviso = new AvisoParaOrden(
            Guid.NewGuid(), Guid.NewGuid(), "Artículo sintético", 10m, "BOB");
        var repositorio = new RepositorioFake { Existe = true };
        var handler = new SolicitarOrdenCommandHandler(
            repositorio,
            new ConsultaAvisoFake(aviso),
            new VerificacionFake(true));

        var resultado = await handler.Handle(
            new SolicitarOrdenCommand(aviso.AvisoId, Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.Duplicada, resultado.Error.Code);
    }

    private sealed class RepositorioFake : IRepositorioOrdenes
    {
        public bool Existe { get; init; }

        public Orden? Agregada { get; private set; }

        public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct) =>
            Task.FromResult(Existe);

        public Task<Orden?> ObtenerAsync(Guid ordenId, CancellationToken ct) =>
            Task.FromResult<Orden?>(null);

        public void Agregar(Orden orden) => Agregada = orden;
    }

    private sealed class ConsultaAvisoFake(AvisoParaOrden? aviso) : IConsultaAvisoParaOrden
    {
        public Task<AvisoParaOrden?> ObtenerAsync(Guid avisoId, CancellationToken ct) =>
            Task.FromResult(aviso);
    }

    private sealed class VerificacionFake(bool verificado) : IConsultaVerificacionParticipante
    {
        public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(verificado);
    }
}
