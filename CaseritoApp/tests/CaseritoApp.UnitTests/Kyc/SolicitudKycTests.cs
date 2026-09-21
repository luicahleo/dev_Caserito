using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class SolicitudKycTests
{
    [Fact]
    public void Solicitud_nueva_no_tiene_motivo_de_revision()
    {
        var verificacion = VerificacionKyc.Crear(Guid.NewGuid());
        var solicitud = verificacion.EnviarSolicitud(
            "doc", "selfie", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow).Valor;

        Assert.Null(solicitud.MotivoRevision);
    }

    [Fact]
    public void Registrar_motivo_de_revision_lo_conserva()
    {
        var verificacion = VerificacionKyc.Crear(Guid.NewGuid());
        var solicitud = verificacion.EnviarSolicitud(
            "doc", "selfie", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow).Valor;

        solicitud.RegistrarMotivoRevision(MotivoRevisionKyc.RostroNoDetectado);

        Assert.Equal(MotivoRevisionKyc.RostroNoDetectado, solicitud.MotivoRevision);
    }
}
