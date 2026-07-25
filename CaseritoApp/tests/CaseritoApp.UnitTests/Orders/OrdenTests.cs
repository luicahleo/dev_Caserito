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

    [Theory]
    [InlineData(true)]  // comprador
    [InlineData(false)] // vendedor
    public void Cancelar_desde_requested_por_participante_cambia_estado_y_emite_evento(bool porComprador)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        orden.LimpiarEventos();
        var actorId = porComprador ? compradorId : vendedorId;
        var ocurrioEn = DateTimeOffset.Now;

        var resultado = orden.Cancelar(actorId, ocurrioEn);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
        Assert.Equal(ocurrioEn.ToUniversalTime(), orden.ActualizadaEn);
        var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
        Assert.Equal(EstadoOrden.Requested, evento.EstadoAnterior);
        Assert.Equal(EstadoOrden.Cancelled, evento.EstadoNuevo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cancelar_desde_agreed_por_participante_cambia_estado_y_emite_evento(bool porComprador)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var actorId = porComprador ? compradorId : vendedorId;

        var resultado = orden.Cancelar(actorId, DateTimeOffset.UtcNow);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
        var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
        Assert.Equal(EstadoOrden.Agreed, evento.EstadoAnterior);
        Assert.Equal(EstadoOrden.Cancelled, evento.EstadoNuevo);
    }

    [Fact]
    public void Cancelar_por_tercero_oculta_la_orden()
    {
        var orden = CrearOrden(Guid.NewGuid());
        orden.LimpiarEventos();

        var resultado = orden.Cancelar(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Equal(EstadoOrden.Requested, orden.Estado);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Cancelar_reintento_es_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Cancelar(compradorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();

        var resultado = orden.Cancelar(vendedorId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Cancelar_con_actor_vacio_oculta_la_orden()
    {
        var orden = CrearOrden(Guid.NewGuid());

        var resultado = orden.Cancelar(Guid.Empty, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public void Marcar_como_vendida_por_vendedor_cierra_su_confirmacion()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var ocurrioEn = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.FromHours(-4));

        var resultado = orden.MarcarComoVendida(vendedorId, ocurrioEn);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.MarkedAsSold, orden.Estado);
        Assert.Equal(ocurrioEn.ToUniversalTime(), orden.MarcadaVendidaEn);
        Assert.Equal(orden.MarcadaVendidaEn, orden.ActualizadaEn);
        var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
        Assert.Equal(EstadoOrden.Agreed, evento.EstadoAnterior);
        Assert.Equal(EstadoOrden.MarkedAsSold, evento.EstadoNuevo);
    }

    [Theory]
    [InlineData("comprador")]
    [InlineData("tercero")]
    [InlineData("vacio")]
    public void Marcar_como_vendida_por_actor_incorrecto_oculta_la_orden(string actor)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var actorId = actor switch
        {
            "comprador" => compradorId,
            "tercero" => Guid.NewGuid(),
            _ => Guid.Empty,
        };

        var resultado = orden.MarcarComoVendida(actorId, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Equal(EstadoOrden.Agreed, orden.Estado);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Marcar_como_vendida_rechaza_estado_anterior_invalido(bool cancelada)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        if (cancelada)
        {
            Assert.True(orden.Cancelar(compradorId, DateTimeOffset.UtcNow).EsExito);
        }

        orden.LimpiarEventos();

        var resultado = orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.TransicionInvalida, resultado.Error.Code);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Confirmar_cierre_por_comprador_completa_el_acuerdo()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var ocurrioEn = new DateTimeOffset(2026, 7, 25, 13, 0, 0, TimeSpan.FromHours(-4));

        var resultado = orden.ConfirmarCierre(compradorId, ocurrioEn);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Completed, orden.Estado);
        Assert.Equal(ocurrioEn.ToUniversalTime(), orden.CompradorConfirmoEn);
        Assert.Equal(orden.CompradorConfirmoEn, orden.CompletadaEn);
        Assert.Equal(orden.CompletadaEn, orden.ActualizadaEn);
        var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
        Assert.Equal(EstadoOrden.MarkedAsSold, evento.EstadoAnterior);
        Assert.Equal(EstadoOrden.Completed, evento.EstadoNuevo);
    }

    [Theory]
    [InlineData("vendedor")]
    [InlineData("tercero")]
    [InlineData("vacio")]
    public void Confirmar_cierre_por_actor_incorrecto_oculta_la_orden(string actor)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();
        var actorId = actor switch
        {
            "vendedor" => vendedorId,
            "tercero" => Guid.NewGuid(),
            _ => Guid.Empty,
        };

        var resultado = orden.ConfirmarCierre(actorId, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Equal(EstadoOrden.MarkedAsSold, orden.Estado);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("agreed")]
    [InlineData("cancelled")]
    public void Confirmar_cierre_rechaza_estados_anteriores(string estado)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        if (estado == "agreed")
        {
            Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        }
        else if (estado == "cancelled")
        {
            Assert.True(orden.Cancelar(compradorId, DateTimeOffset.UtcNow).EsExito);
        }

        orden.LimpiarEventos();

        var resultado = orden.ConfirmarCierre(compradorId, DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.TransicionInvalida, resultado.Error.Code);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Reintentos_de_cierre_son_idempotentes_y_fechas_permanecen()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        var marcadaVendidaEn = orden.MarcadaVendidaEn;
        orden.LimpiarEventos();

        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow.AddDays(1)).EsExito);
        Assert.Equal(marcadaVendidaEn, orden.MarcadaVendidaEn);
        Assert.Empty(orden.EventosDeDominio);

        Assert.True(orden.ConfirmarCierre(compradorId, DateTimeOffset.UtcNow).EsExito);
        var completadaEn = orden.CompletadaEn;
        orden.LimpiarEventos();

        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow.AddDays(2)).EsExito);
        Assert.True(orden.ConfirmarCierre(compradorId, DateTimeOffset.UtcNow.AddDays(2)).EsExito);
        Assert.Equal(marcadaVendidaEn, orden.MarcadaVendidaEn);
        Assert.Equal(completadaEn, orden.CompletadaEn);
        Assert.Empty(orden.EventosDeDominio);
    }

    [Fact]
    public void Aceptar_y_cancelar_rechazan_estados_de_cierre()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrdenCon(compradorId, vendedorId);
        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(orden.MarcarComoVendida(vendedorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();

        var aceptarVendida = orden.Aceptar(vendedorId, DateTimeOffset.UtcNow);
        var cancelarVendida = orden.Cancelar(compradorId, DateTimeOffset.UtcNow);

        Assert.False(aceptarVendida.EsExito);
        Assert.False(cancelarVendida.EsExito);
        Assert.Equal(ErroresOrden.TransicionInvalida, aceptarVendida.Error.Code);
        Assert.Equal(ErroresOrden.TransicionInvalida, cancelarVendida.Error.Code);

        Assert.True(orden.ConfirmarCierre(compradorId, DateTimeOffset.UtcNow).EsExito);
        orden.LimpiarEventos();

        Assert.Equal(
            ErroresOrden.TransicionInvalida,
            orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).Error.Code);
        Assert.Equal(
            ErroresOrden.TransicionInvalida,
            orden.Cancelar(compradorId, DateTimeOffset.UtcNow).Error.Code);
        Assert.Empty(orden.EventosDeDominio);
    }

    private static Orden CrearOrden(Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), Guid.NewGuid(), vendedorId, 10m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        return resultado.Valor;
    }

    private static Orden CrearOrdenCon(Guid compradorId, Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), compradorId, vendedorId, 10m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        return resultado.Valor;
    }
}
