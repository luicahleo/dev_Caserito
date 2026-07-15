using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class VerificacionKycTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static VerificacionKyc ConSolicitudPendiente(out Guid solicitudId)
    {
        var v = VerificacionKyc.Crear(Guid.NewGuid());
        var r = v.EnviarSolicitud("doc", "selfie", TipoDocumento.CedulaIdentidad, _t0);
        solicitudId = r.Valor.Id;
        return v;
    }

    [Fact]
    public void Enviar_primera_solicitud_queda_pendiente_y_es_la_actual()
    {
        var v = VerificacionKyc.Crear(Guid.NewGuid());

        var r = v.EnviarSolicitud("doc", "selfie", TipoDocumento.CedulaIdentidad, _t0);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Pendiente, v.SolicitudActual!.Estado);
        Assert.False(v.EstaVerificado);
    }

    [Fact]
    public void Enviar_con_solicitud_pendiente_falla()
    {
        var v = ConSolicitudPendiente(out _);

        var r = v.EnviarSolicitud("doc2", "selfie2", TipoDocumento.CedulaIdentidad, _t0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
    }

    [Fact]
    public void Aprobar_pendiente_marca_verificado()
    {
        var v = ConSolicitudPendiente(out var id);
        var revisor = Guid.NewGuid();

        var r = v.Aprobar(id, revisor, _t0);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Aprobada, v.SolicitudActual!.Estado);
        Assert.Equal(revisor, v.SolicitudActual.ResueltaPor);
        Assert.True(v.EstaVerificado);
    }

    [Fact]
    public void Enviar_tras_aprobado_falla_con_YaVerificado()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Aprobar(id, Guid.NewGuid(), _t0);

        var r = v.EnviarSolicitud("doc3", "selfie3", TipoDocumento.CedulaIdentidad, _t0.AddDays(1));

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.YaVerificado, r.Error.Code);
    }

    [Fact]
    public void Rechazar_pendiente_permite_reenviar()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Rechazar(id, Guid.NewGuid(), "Foto borrosa", _t0);

        Assert.Equal(EstadoKyc.Rechazada, v.SolicitudActual!.Estado);
        Assert.Equal("Foto borrosa", v.SolicitudActual.MotivoRechazo);

        var r = v.EnviarSolicitud("doc4", "selfie4", TipoDocumento.CedulaIdentidad, _t0.AddDays(1));

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Pendiente, v.SolicitudActual!.Estado);
        Assert.Equal(2, v.Solicitudes.Count);
    }

    [Fact]
    public void Aprobar_solicitud_inexistente_falla()
    {
        var v = ConSolicitudPendiente(out _);

        var r = v.Aprobar(Guid.NewGuid(), Guid.NewGuid(), _t0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudNoEncontrada, r.Error.Code);
    }

    [Fact]
    public void Aprobar_solicitud_ya_resuelta_falla_con_TransicionInvalida()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Rechazar(id, Guid.NewGuid(), "motivo", _t0);

        var r = v.Aprobar(id, Guid.NewGuid(), _t0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.TransicionInvalida, r.Error.Code);
    }
}
