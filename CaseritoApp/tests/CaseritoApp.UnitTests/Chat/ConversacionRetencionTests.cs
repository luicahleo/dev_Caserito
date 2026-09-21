using CaseritoApp.Chat.Domain.Conversaciones;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ConversacionRetencionTests
{
    private static readonly Guid _aviso = Guid.NewGuid();
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();
    private static readonly DateTimeOffset _ahora = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private static Conversacion Retenida() =>
        Conversacion.Crear(_aviso, _comprador, _vendedor, _ahora, retenida: true).Valor;

    private static Conversacion Activa() =>
        Conversacion.Crear(_aviso, _comprador, _vendedor, _ahora).Valor;

    [Fact]
    public void Puede_nacer_retenida()
    {
        Assert.Equal(EstadoConversacion.RetenidaPorVerificacion, Retenida().Estado);
    }

    [Fact]
    public void Por_defecto_nace_activa()
    {
        Assert.Equal(EstadoConversacion.Activa, Activa().Estado);
    }

    [Fact]
    public void Liberar_la_pasa_a_activa()
    {
        var conversacion = Retenida();

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Liberar_dos_veces_es_idempotente()
    {
        var conversacion = Retenida();
        conversacion.LiberarPorVerificacion(_ahora);

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Liberar_una_conversacion_cerrada_falla()
    {
        var conversacion = Retenida();
        conversacion.LiberarPorVerificacion(_ahora);
        Assert.True(conversacion.CerrarPorParticipante(_comprador, _ahora).EsExito);

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
    }

    [Fact]
    public void El_comprador_puede_escribir_estando_retenida()
    {
        var conversacion = Retenida();

        var resultado = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 1, "hola", _ahora);

        Assert.True(resultado.EsExito);
    }

    [Fact]
    public void El_vendedor_no_puede_escribir_estando_retenida()
    {
        var conversacion = Retenida();

        var resultado = conversacion.CrearMensaje(
            _vendedor, Guid.NewGuid(), 1, "hola", _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
    }
}
