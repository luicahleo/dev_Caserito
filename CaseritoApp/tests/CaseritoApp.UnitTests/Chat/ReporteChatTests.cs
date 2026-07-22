using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ReporteChatTests
{
    private static readonly DateTimeOffset _ahora =
        new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reporte_de_conversacion_nace_pendiente_y_normaliza_el_detalle()
    {
        var conversacionId = Guid.NewGuid();
        var reportanteId = Guid.NewGuid();

        var resultado = ReporteChat.Crear(
            conversacionId,
            reportanteId,
            TipoObjetivoReporteChat.Conversacion,
            null,
            CategoriaReporteChat.Acoso,
            "  Contexto  ",
            _ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(conversacionId, resultado.Valor.ConversacionId);
        Assert.Equal(reportanteId, resultado.Valor.ReportanteId);
        Assert.Equal(EstadoReporteChat.Pendiente, resultado.Valor.Estado);
        Assert.Equal("Contexto", resultado.Valor.Detalle);
        Assert.Null(resultado.Valor.MensajeId);
    }

    [Fact]
    public void Objetivo_mensaje_exige_mensaje_y_los_otros_lo_prohiben()
    {
        var sinMensaje = Crear(TipoObjetivoReporteChat.Mensaje, null);
        var conversacionConMensaje = Crear(TipoObjetivoReporteChat.Conversacion, Guid.NewGuid());
        var participanteConMensaje = Crear(TipoObjetivoReporteChat.Participante, Guid.NewGuid());

        Assert.False(sinMensaje.EsExito);
        Assert.False(conversacionConMensaje.EsExito);
        Assert.False(participanteConMensaje.EsExito);
    }

    [Fact]
    public void Tomar_y_atender_registran_al_moderador_asignado()
    {
        var moderadorId = Guid.NewGuid();
        var reporte = Crear(TipoObjetivoReporteChat.Conversacion, null).Valor;

        var toma = reporte.Tomar(moderadorId, _ahora.AddMinutes(1));
        var resolucion = reporte.Atender(moderadorId, _ahora.AddMinutes(2));

        Assert.True(toma.EsExito);
        Assert.True(resolucion.EsExito);
        Assert.Equal(EstadoReporteChat.Atendido, reporte.Estado);
        Assert.Equal(moderadorId, reporte.ModeradorAsignadoId);
        Assert.Equal(_ahora.AddMinutes(1), reporte.TomadoEn);
        Assert.Equal(_ahora.AddMinutes(2), reporte.ResueltoEn);
    }

    [Fact]
    public void Solo_el_moderador_asignado_puede_liberar_o_resolver()
    {
        var asignadoId = Guid.NewGuid();
        var otroId = Guid.NewGuid();
        var reporte = Crear(TipoObjetivoReporteChat.Participante, null).Valor;
        reporte.Tomar(asignadoId, _ahora);

        var liberar = reporte.Liberar(otroId);
        var atender = reporte.Atender(otroId, _ahora);
        var descartar = reporte.Descartar(otroId, _ahora);

        Assert.All([liberar, atender, descartar], resultado =>
            Assert.Equal(ErroresModeracionChat.TransicionInvalida, resultado.Error.Code));
        Assert.Equal(EstadoReporteChat.EnRevision, reporte.Estado);
        Assert.Equal(asignadoId, reporte.ModeradorAsignadoId);
    }

    [Fact]
    public void Liberar_vuelve_a_pendiente_y_permite_otra_asignacion()
    {
        var primeroId = Guid.NewGuid();
        var segundoId = Guid.NewGuid();
        var reporte = Crear(TipoObjetivoReporteChat.Conversacion, null).Valor;
        reporte.Tomar(primeroId, _ahora);

        var liberacion = reporte.Liberar(primeroId);
        var nuevaToma = reporte.Tomar(segundoId, _ahora.AddMinutes(1));
        var descarte = reporte.Descartar(segundoId, _ahora.AddMinutes(2));

        Assert.True(liberacion.EsExito);
        Assert.True(nuevaToma.EsExito);
        Assert.True(descarte.EsExito);
        Assert.Equal(EstadoReporteChat.Descartado, reporte.Estado);
        Assert.Equal(segundoId, reporte.ModeradorAsignadoId);
    }

    [Fact]
    public void Detalle_mayor_a_mil_caracteres_y_estado_final_se_rechazan()
    {
        var invalido = ReporteChat.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoObjetivoReporteChat.Conversacion,
            null,
            CategoriaReporteChat.Otro,
            new string('a', 1001),
            _ahora);
        var reporte = Crear(TipoObjetivoReporteChat.Mensaje, Guid.NewGuid()).Valor;
        var moderadorId = Guid.NewGuid();
        reporte.Tomar(moderadorId, _ahora);
        reporte.Atender(moderadorId, _ahora);

        var nuevaToma = reporte.Tomar(moderadorId, _ahora);

        Assert.False(invalido.EsExito);
        Assert.Equal(ErroresModeracionChat.TransicionInvalida, nuevaToma.Error.Code);
    }

    private static CaseritoApp.BuildingBlocks.Domain.Result<ReporteChat> Crear(
        TipoObjetivoReporteChat tipo,
        Guid? mensajeId) => ReporteChat.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            tipo,
            mensajeId,
            CategoriaReporteChat.Spam,
            null,
            _ahora);
}
