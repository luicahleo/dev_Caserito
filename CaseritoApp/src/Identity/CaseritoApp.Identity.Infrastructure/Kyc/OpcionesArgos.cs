namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Configuración de conexión al servicio ARGOS.</summary>
public sealed class OpcionesArgos
{
    public const string Seccion = "Argos";

    /// <summary>URL base de ARGOS, sin barra final.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>API key para el header X-Service-Key. Opcional.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Similitud mínima (0-100) para aprobar una verificación sin revisión humana. ARGOS corta la
    /// no-coincidencia en 32; este umbral es el corte superior propio de la aplicación.
    /// </summary>
    public double UmbralAutoAprobacion { get; set; } = 60;
}
