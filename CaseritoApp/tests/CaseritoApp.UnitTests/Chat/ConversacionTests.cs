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
        Assert.True(conversacion.EsParticipante(compradorId));
        Assert.True(conversacion.EsParticipante(vendedorId));
        Assert.False(conversacion.EsParticipante(Guid.NewGuid()));
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
}
