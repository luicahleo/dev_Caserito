namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Configuración de conexión al servicio ARGOS.</summary>
public sealed class OpcionesArgos
{
    public const string Seccion = "Argos";

    /// <summary>URL base de ARGOS, sin barra final.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>API key para el header X-Service-Key. Opcional.</summary>
    public string? ApiKey { get; set; }
}
