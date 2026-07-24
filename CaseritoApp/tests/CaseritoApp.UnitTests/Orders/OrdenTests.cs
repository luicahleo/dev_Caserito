using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class OrdenTests
{
    [Fact]
    public void Crear_valida_y_congela_el_acuerdo()
    {
        var avisoId = Guid.NewGuid();
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var ocurrioEn = new DateTimeOffset(2026, 7, 24, 18, 0, 0, TimeSpan.FromHours(-4));

        var resultado = Orden.Crear(
            avisoId, compradorId, vendedorId, 125.50m, "BOB", ocurrioEn);

        Assert.True(resultado.EsExito);
        var orden = resultado.Valor;
        Assert.Equal(avisoId, orden.AvisoId);
        Assert.Equal(compradorId, orden.CompradorId);
        Assert.Equal(vendedorId, orden.VendedorId);
        Assert.Equal(125.50m, orden.MontoAcordado);
        Assert.Equal("BOB", orden.Moneda);
        Assert.Equal(EstadoOrden.Requested, orden.Estado);
        Assert.Equal(ocurrioEn.ToUniversalTime(), orden.CreadaEn);
        Assert.Equal(orden.CreadaEn, orden.ActualizadaEn);
        Assert.IsType<OrdenSolicitada>(Assert.Single(orden.EventosDeDominio));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_rechaza_monto_no_positivo(decimal monto)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), monto, "BOB", DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.AcuerdoInvalido, resultado.Error.Code);
    }

    [Fact]
    public void Crear_rechaza_participantes_iguales()
    {
        var participanteId = Guid.NewGuid();

        var resultado = Orden.Crear(
            Guid.NewGuid(), participanteId, participanteId, 10m, "BOB", DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.ParticipantesCoinciden, resultado.Error.Code);
    }

    [Fact]
    public void Aceptar_por_vendedor_cambia_estado_y_emite_evento()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(vendedorId);
        orden.LimpiarEventos();
        var ocurrioEn = DateTimeOffset.Now;

        var resultado = orden.Aceptar(vendedorId, ocurrioEn);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Agreed, orden.Estado);
        Assert.Equal(ocurrioEn.ToUniversalTime(), orden.ActualizadaEn);
        var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
        Assert.Equal(EstadoOrden.Requested, evento.EstadoAnterior);
        Assert.Equal(EstadoOrden.Agreed, evento.EstadoNuevo);
    }

    [Fact]
    public void Aceptar_reintento_es_idempotente()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();

        var resultado = orden.Aceptar(vendedorId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(resultado.EsExito);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Aceptar_por_tercero_oculta_la_orden()
    {
        var orden = CrearOrden(Guid.NewGuid());

        var resultado = orden.Aceptar(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Equal(EstadoOrden.Requested, orden.Estado);
    }

    private static Orden CrearOrden(Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), Guid.NewGuid(), vendedorId, 10m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        return resultado.Valor;
    }
}
