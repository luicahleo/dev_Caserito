using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.UnitTests.Chat;

public sealed class RegistroModeracionChatTests
{
    [Fact]
    public void Registro_conserva_solo_referencias_accion_y_fecha()
    {
        var reporteId = Guid.NewGuid();
        var conversacionId = Guid.NewGuid();
        var moderadorId = Guid.NewGuid();
        var ahora = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

        var resultado = RegistroModeracionChat.Crear(
            reporteId,
            conversacionId,
            moderadorId,
            AccionModeracionChat.ConsultarEvidencia,
            ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(reporteId, resultado.Valor.ReporteId);
        Assert.Equal(conversacionId, resultado.Valor.ConversacionId);
        Assert.Equal(moderadorId, resultado.Valor.ModeradorId);
        Assert.Equal(AccionModeracionChat.ConsultarEvidencia, resultado.Valor.Accion);
        Assert.Equal(ahora, resultado.Valor.CreadoEn);

        var nombres = typeof(RegistroModeracionChat).GetProperties()
            .Select(p => p.Name)
            .ToArray();
        Assert.DoesNotContain(nombres, nombre =>
            nombre.Contains("Texto", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Detalle", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Participante", StringComparison.OrdinalIgnoreCase));
    }
}
