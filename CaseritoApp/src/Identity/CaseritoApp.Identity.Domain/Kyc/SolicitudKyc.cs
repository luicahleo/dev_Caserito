using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Un intento de verificación: referencias (opacas) a los blobs de documento y selfie, su estado y
/// la resolución del revisor. Los bytes de las imágenes nunca entran al dominio.
/// </summary>
public sealed class SolicitudKyc : Entity
{
    // Constructor para EF Core.
    private SolicitudKyc()
    {
        ReferenciaDocumento = string.Empty;
        ReferenciaSelfie = string.Empty;
    }

    internal SolicitudKyc(
        string referenciaDocumento, string referenciaSelfie, TipoDocumento tipoDocumento, DateTimeOffset enviadaEn)
    {
        ReferenciaDocumento = referenciaDocumento;
        ReferenciaSelfie = referenciaSelfie;
        TipoDocumento = tipoDocumento;
        EnviadaEn = enviadaEn;
        Estado = EstadoKyc.Pendiente;
    }

    public EstadoKyc Estado { get; private set; }
    public string ReferenciaDocumento { get; private init; }
    public string ReferenciaSelfie { get; private init; }
    public TipoDocumento TipoDocumento { get; private init; }
    public string? MotivoRechazo { get; private set; }
    public DateTimeOffset EnviadaEn { get; private init; }
    public DateTimeOffset? ResueltaEn { get; private set; }
    public Guid? ResueltaPor { get; private set; }

    internal void MarcarAprobada(Guid revisorId, DateTimeOffset cuando)
    {
        Estado = EstadoKyc.Aprobada;
        ResueltaPor = revisorId;
        ResueltaEn = cuando;
    }

    internal void MarcarRechazada(Guid revisorId, string motivo, DateTimeOffset cuando)
    {
        Estado = EstadoKyc.Rechazada;
        MotivoRechazo = motivo;
        ResueltaPor = revisorId;
        ResueltaEn = cuando;
    }
}
