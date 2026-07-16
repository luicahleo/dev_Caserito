namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Contenido descifrado de un blob KYC más su tipo MIME.</summary>
public sealed record BlobKyc(byte[] Contenido, string ContentType);

/// <summary>Estado de verificación efectivo del usuario para exposición al propio usuario.</summary>
public sealed record EstadoKycDto(string Estado, string? MotivoRechazo);

/// <summary>Metadatos de una solicitud para el listado del administrador (sin blobs).</summary>
public sealed record SolicitudKycResumenDto(
    Guid SolicitudId,
    Guid UsuarioId,
    string Estado,
    string TipoDocumento,
    DateTimeOffset EnviadaEn,
    DateTimeOffset? ResueltaEn);
