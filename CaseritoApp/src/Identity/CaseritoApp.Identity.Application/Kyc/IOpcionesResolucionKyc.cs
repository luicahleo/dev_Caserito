namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Umbral de similitud a partir del cual una verificación se aprueba sin intervención humana.
/// Es un puerto porque Application no referencia paquetes de configuración.
/// </summary>
public interface IOpcionesResolucionKyc
{
    /// <summary>Similitud mínima (0-100) para aprobar automáticamente. Inclusivo.</summary>
    public double UmbralAutoAprobacionSimilitud { get; }
}
