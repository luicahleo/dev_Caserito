using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class ConsultasOrdenesHandlerTests
{
    [Fact]
    public async Task Listar_restringe_la_consulta_al_actor()
    {
        var actorId = Guid.NewGuid();
        var consulta = new ConsultaFake();
        var handler = new ListarOrdenesQueryHandler(consulta);

        await handler.Handle(
            new ListarOrdenesQuery(actorId, "comprador", "Requested", 2, 15),
            CancellationToken.None);

        Assert.Equal(actorId, consulta.ActorListado);
        Assert.Equal("comprador", consulta.Rol);
        Assert.Equal(EstadoOrden.Requested, consulta.Estado);
        Assert.Equal(2, consulta.Pagina);
        Assert.Equal(15, consulta.Tamano);
    }

    [Fact]
    public async Task Obtener_restringe_el_detalle_al_actor()
    {
        var actorId = Guid.NewGuid();
        var ordenId = Guid.NewGuid();
        var consulta = new ConsultaFake();
        var handler = new ObtenerOrdenQueryHandler(consulta);

        await handler.Handle(
            new ObtenerOrdenQuery(ordenId, actorId),
            CancellationToken.None);

        Assert.Equal(ordenId, consulta.OrdenId);
        Assert.Equal(actorId, consulta.ActorDetalle);
    }

    [Theory]
    [InlineData("", "Requested", 1, 20)]
    [InlineData("tercero", "Requested", 1, 20)]
    [InlineData("comprador", "Desconocido", 1, 20)]
    [InlineData("comprador", "Requested", 0, 20)]
    [InlineData("comprador", "Requested", 1, 101)]
    public void Listar_rechaza_filtros_o_paginacion_invalidos(
        string rol,
        string? estado,
        int pagina,
        int tamano)
    {
        var resultado = new ListarOrdenesQueryValidator().Validate(
            new ListarOrdenesQuery(Guid.NewGuid(), rol, estado, pagina, tamano));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Obtener_rechaza_identificadores_vacios()
    {
        var resultado = new ObtenerOrdenQueryValidator().Validate(
            new ObtenerOrdenQuery(Guid.Empty, Guid.Empty));

        Assert.False(resultado.IsValid);
        Assert.Equal(2, resultado.Errors.Count);
    }

    [Theory]
    [InlineData("MarkedAsSold")]
    [InlineData("Completed")]
    public void Listar_admite_estados_de_cierre(string estado)
    {
        var resultado = new ListarOrdenesQueryValidator().Validate(
            new ListarOrdenesQuery(Guid.NewGuid(), "comprador", estado, 1, 20));

        Assert.True(resultado.IsValid);
    }

    private sealed class ConsultaFake : IConsultaOrdenes
    {
        public Guid ActorListado { get; private set; }

        public string? Rol { get; private set; }

        public EstadoOrden? Estado { get; private set; }

        public int Pagina { get; private set; }

        public int Tamano { get; private set; }

        public Guid OrdenId { get; private set; }

        public Guid ActorDetalle { get; private set; }

        public Task<ResultadoPaginadoOrdenes> ListarAsync(
            Guid actorId,
            string rol,
            EstadoOrden? estado,
            int pagina,
            int tamano,
            CancellationToken ct)
        {
            ActorListado = actorId;
            Rol = rol;
            Estado = estado;
            Pagina = pagina;
            Tamano = tamano;
            return Task.FromResult(new ResultadoPaginadoOrdenes([], pagina, tamano, 0));
        }

        public Task<OrdenDetalleDto?> ObtenerAsync(
            Guid ordenId,
            Guid actorId,
            CancellationToken ct)
        {
            OrdenId = ordenId;
            ActorDetalle = actorId;
            return Task.FromResult<OrdenDetalleDto?>(null);
        }
    }
}
