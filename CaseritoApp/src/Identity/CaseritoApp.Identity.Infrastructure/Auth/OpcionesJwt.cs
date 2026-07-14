namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Opciones de configuración para la emisión y validación de JWT de acceso.</summary>
public sealed class OpcionesJwt
{
    /// <summary>Nombre de la sección de configuración (<c>Jwt</c>).</summary>
    public const string Seccion = "Jwt";

    /// <summary>Clave simétrica usada para firmar (HS256). Debe tener al menos 32 bytes (256 bits).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Emisor del token.</summary>
    public string Issuer { get; set; } = "CaseritoApp";

    /// <summary>Audiencia del token.</summary>
    public string Audience { get; set; } = "CaseritoApp";

    /// <summary>Minutos de vigencia del access token.</summary>
    public int MinutosAcceso { get; set; } = 15;
}
