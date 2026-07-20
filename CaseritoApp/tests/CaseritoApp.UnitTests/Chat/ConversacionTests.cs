using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ConversacionTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_registra_participantes_aviso_y_fecha()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();

        var resultado = Conversacion.Crear(avisoId, compradorId, vendedorId, _ahora);

        Assert.True(resultado.EsExito);
        var conversacion = resultado.Valor;
        Assert.Equal(avisoId, conversacion.AvisoId);
        Assert.Equal(compradorId, conversacion.CompradorId);
        Assert.Equal(vendedorId, conversacion.VendedorId);
        Assert.Equal(_ahora, conversacion.CreadaEn);
        Assert.Equal(_ahora, conversacion.UltimaActividadEn);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
        Assert.True(conversacion.EsParticipante(compradorId));
        Assert.True(conversacion.EsParticipante(vendedorId));
        Assert.False(conversacion.EsParticipante(Guid.NewGuid()));
    }

    [Fact]
    public void Participante_cierra_y_reabre_de_forma_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var cierre = conversacion.CerrarPorParticipante(compradorId, _ahora.AddMinutes(1));
        var repeticion = conversacion.CerrarPorParticipante(compradorId, _ahora.AddMinutes(2));
        var reapertura = conversacion.ReabrirPorParticipante(compradorId, _ahora.AddMinutes(3));
        var segundaReapertura = conversacion.ReabrirPorParticipante(compradorId, _ahora.AddMinutes(4));

        Assert.True(cierre.EsExito);
        Assert.Equal(compradorId, conversacion.UltimoActorEstadoId);
        Assert.True(repeticion.EsExito);
        Assert.True(reapertura.EsExito);
        Assert.True(segundaReapertura.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
        Assert.Null(conversacion.CerradaEn);
        Assert.Equal(2, conversacion.EventosDeDominio.OfType<ConversacionEstadoCambiado>().Count());
    }

    [Fact]
    public void Tercero_no_puede_cerrar_ni_reabrir()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;

        var cierre = conversacion.CerrarPorParticipante(Guid.NewGuid(), _ahora.AddMinutes(1));
        var reapertura = conversacion.ReabrirPorParticipante(Guid.NewGuid(), _ahora.AddMinutes(2));

        Assert.Equal(ErroresConversacion.NoEncontrada, cierre.Error.Code);
        Assert.Equal(ErroresConversacion.NoEncontrada, reapertura.Error.Code);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Solo_moderacion_revierte_cierre_de_moderacion()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var moderadorId = Guid.NewGuid();
        Assert.True(conversacion.CerrarPorModeracion(moderadorId, _ahora.AddMinutes(1)).EsExito);

        var participante = conversacion.ReabrirPorParticipante(compradorId, _ahora.AddMinutes(2));
        var moderacion = conversacion.ReabrirPorModeracion(moderadorId, _ahora.AddMinutes(3));
        var repeticion = conversacion.ReabrirPorModeracion(moderadorId, _ahora.AddMinutes(4));

        Assert.False(participante.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, participante.Error.Code);
        Assert.True(moderacion.EsExito);
        Assert.True(repeticion.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrearMensaje_rechaza_conversacion_cerrada_con_error_unico(bool porModeracion)
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;
        if (porModeracion)
        {
            conversacion.CerrarPorModeracion(Guid.NewGuid(), _ahora.AddMinutes(1));
        }
        else
        {
            conversacion.CerrarPorParticipante(compradorId, _ahora.AddMinutes(1));
        }

        var resultado = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 1, "No debe enviarse", _ahora.AddMinutes(2));

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
    }

    [Theory]
    [InlineData("aviso")]
    [InlineData("comprador")]
    [InlineData("vendedor")]
    public void Crear_rechaza_identificadores_vacios(string campo)
    {
        var avisoId = campo == "aviso" ? Guid.Empty : Guid.NewGuid();
        var compradorId = campo == "comprador" ? Guid.Empty : Guid.NewGuid();
        var vendedorId = campo == "vendedor" ? Guid.Empty : Guid.NewGuid();

        var resultado = Conversacion.Crear(avisoId, compradorId, vendedorId, _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.IdentificadorInvalido, resultado.Error.Code);
    }

    [Fact]
    public void Crear_rechaza_comprador_igual_a_vendedor()
    {
        var participanteId = Guid.NewGuid();

        var resultado = Conversacion.Crear(Guid.NewGuid(), participanteId, participanteId, _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.ParticipantesCoinciden, resultado.Error.Code);
    }

    [Fact]
    public void Crear_emite_evento_sin_datos_de_perfil()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();

        var conversacion = Conversacion.Crear(avisoId, compradorId, vendedorId, _ahora).Valor;

        var evento = Assert.IsType<ConversacionIniciada>(Assert.Single(conversacion.EventosDeDominio));
        Assert.Equal(conversacion.Id, evento.ConversacionId);
        Assert.Equal(avisoId, evento.AvisoId);
        Assert.Equal(_ahora, evento.OcurridoEn);
        Assert.DoesNotContain(
            evento.GetType().GetProperties(),
            propiedad => propiedad.PropertyType == typeof(string));
    }

    [Fact]
    public void MarcarLectura_avanza_sin_retroceder_y_es_idempotente()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        conversacion.CrearMensaje(vendedorId, Guid.NewGuid(), 5, "Hola", _ahora.AddMinutes(1));
        conversacion.LimpiarEventos();

        var avance = conversacion.MarcarLectura(compradorId, 5, _ahora.AddMinutes(2));
        var repeticion = conversacion.MarcarLectura(compradorId, 5, _ahora.AddMinutes(3));
        var retroceso = conversacion.MarcarLectura(compradorId, 3, _ahora.AddMinutes(4));

        Assert.True(avance.EsExito);
        Assert.True(repeticion.EsExito);
        Assert.True(retroceso.EsExito);
        Assert.Equal(5, conversacion.UltimaSecuenciaLeidaComprador);
        Assert.Equal(0, conversacion.UltimaSecuenciaLeidaVendedor);
        Assert.IsType<LecturaAvanzada>(Assert.Single(conversacion.EventosDeDominio));
    }

    [Fact]
    public void MarcarLectura_rechaza_tercero_y_secuencia_inexistente()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;
        conversacion.CrearMensaje(compradorId, Guid.NewGuid(), 2, "Hola", _ahora.AddMinutes(1));

        var tercero = conversacion.MarcarLectura(Guid.NewGuid(), 1, _ahora.AddMinutes(2));
        var inexistente = conversacion.MarcarLectura(compradorId, 3, _ahora.AddMinutes(2));

        Assert.Equal(ErroresConversacion.NoEncontrada, tercero.Error.Code);
        Assert.Equal(ErroresConversacion.SecuenciaInvalida, inexistente.Error.Code);
    }
}
