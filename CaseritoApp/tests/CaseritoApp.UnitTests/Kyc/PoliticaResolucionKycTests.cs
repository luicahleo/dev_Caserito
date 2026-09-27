using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class PoliticaResolucionKycTests
{
    private const double Umbral = 60;

    private static EntradaResolucionKyc Ok(double score) =>
        new(ServicioRespondio: true, RostroDetectado: true, Coinciden: true, Score: score);

    [Fact]
    public void Score_por_encima_del_umbral_aprueba_automaticamente()
    {
        var decision = PoliticaResolucionKyc.Decidir(Ok(85), Umbral);

        Assert.Equal(ResolucionKyc.AprobarAutomatico, decision.Resolucion);
        Assert.Null(decision.Motivo);
    }

    [Fact]
    public void Score_igual_al_umbral_aprueba_automaticamente()
    {
        // El límite es inclusivo por decisión del spec; este test lo fija.
        var decision = PoliticaResolucionKyc.Decidir(Ok(60), Umbral);

        Assert.Equal(ResolucionKyc.AprobarAutomatico, decision.Resolucion);
    }

    [Fact]
    public void Score_por_debajo_del_umbral_va_a_revision()
    {
        var decision = PoliticaResolucionKyc.Decidir(Ok(59.9), Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, decision.Motivo);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(10)]
    public void Sin_coincidencia_rechaza_automaticamente_sea_cual_sea_el_score(double score)
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: true, Coinciden: false, Score: score);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.RechazarAutomatico, decision.Resolucion);
        Assert.Null(decision.Motivo);
    }

    [Fact]
    public void Rostro_no_detectado_va_a_revision_con_su_motivo()
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: false, Coinciden: false, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.RostroNoDetectado, decision.Motivo);
    }

    [Fact]
    public void Servicio_sin_responder_va_a_revision_con_su_motivo()
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: false, RostroDetectado: false, Coinciden: false, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ServicioNoDisponible, decision.Motivo);
    }

    [Fact]
    public void Coincidencia_sin_score_va_a_revision()
    {
        // Defensa: si ARGOS dijera que coincide pero no diera score, no se aprueba sola.
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: true, Coinciden: true, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, decision.Motivo);
    }
}
