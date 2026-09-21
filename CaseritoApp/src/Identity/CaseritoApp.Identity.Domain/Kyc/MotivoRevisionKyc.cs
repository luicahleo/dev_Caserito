namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Por qué una solicitud quedó esperando revisión humana. Es una categoría técnica: no describe
/// a la persona y puede registrarse en logs.
/// </summary>
public enum MotivoRevisionKyc
{
    /// <summary>El rostro coincide, pero el parecido no alcanza el umbral de aprobación automática.</summary>
    ScoreInsuficiente,

    /// <summary>No se pudo detectar un rostro en alguna de las dos imágenes.</summary>
    RostroNoDetectado,

    /// <summary>El servicio de comparación facial no respondió o falló al procesar.</summary>
    ServicioNoDisponible,
}
