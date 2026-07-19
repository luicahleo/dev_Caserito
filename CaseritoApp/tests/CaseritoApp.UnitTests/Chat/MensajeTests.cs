using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class MensajeTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CrearMensaje_normaliza_extremos_y_conserva_formato_interno()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var resultado = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 1, "  Hola  mundo\n¿Disponible?  ", _ahora.AddMinutes(1));

        Assert.True(resultado.EsExito);
        Assert.Equal("Hola  mundo\n¿Disponible?", resultado.Valor.Texto);
        Assert.Equal(1, resultado.Valor.Secuencia);
        Assert.Equal(_ahora.AddMinutes(1), resultado.Valor.EnviadoEn);
        Assert.Equal(_ahora.AddMinutes(1), conversacion.UltimaActividadEn);
        Assert.Equal(1, conversacion.UltimaSecuencia);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearMensaje_rechaza_texto_vacio(string texto)
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var resultado = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 1, texto, _ahora.AddMinutes(1));

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.TextoInvalido, resultado.Error.Code);
    }

    [Fact]
    public void CrearMensaje_rechaza_texto_mayor_al_limite()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var resultado = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 1, new string('a', 2001), _ahora.AddMinutes(1));

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.TextoInvalido, resultado.Error.Code);
    }

    [Fact]
    public void CrearMensaje_rechaza_remitente_ajeno()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;

        var resultado = conversacion.CrearMensaje(
            Guid.NewGuid(), Guid.NewGuid(), 1, "Hola", _ahora.AddMinutes(1));

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public void CrearMensaje_rechaza_clave_o_secuencia_invalidas()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;

        var claveInvalida = conversacion.CrearMensaje(
            compradorId, Guid.Empty, 1, "Hola", _ahora.AddMinutes(1));
        var secuenciaInvalida = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 0, "Hola", _ahora.AddMinutes(1));

        Assert.Equal(ErroresConversacion.IdentificadorInvalido, claveInvalida.Error.Code);
        Assert.Equal(ErroresConversacion.SecuenciaInvalida, secuenciaInvalida.Error.Code);
    }

    [Fact]
    public void Mensaje_no_expone_setters_publicos_para_contenido()
    {
        var propiedades = typeof(Mensaje).GetProperties()
            .Where(p => p.Name is nameof(Mensaje.Texto) or nameof(Mensaje.RemitenteId)
                or nameof(Mensaje.ClaveIdempotencia) or nameof(Mensaje.EnviadoEn));

        Assert.All(propiedades, propiedad => Assert.False(propiedad.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void CrearMensaje_emite_evento_sin_texto()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;
        conversacion.LimpiarEventos();

        var mensaje = conversacion.CrearMensaje(
            compradorId, Guid.NewGuid(), 1, "contenido sensible", _ahora.AddMinutes(1)).Valor;

        var evento = Assert.IsType<MensajeEnviado>(Assert.Single(conversacion.EventosDeDominio));
        Assert.Equal(mensaje.Id, evento.MensajeId);
        Assert.DoesNotContain(
            evento.GetType().GetProperties(),
            propiedad => propiedad.PropertyType == typeof(string));
    }
}
