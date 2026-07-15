namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Nombres canónicos de los roles del MVP (brief §8.1). Roles ≠ permisos.</summary>
public static class RolesApp
{
    /// <summary>Rol por defecto de cualquier usuario registrado (comprador y vendedor). Sin permisos elevados.</summary>
    public const string Cliente = "Cliente";

    /// <summary>Modera publicaciones y chat reportados (ocultar/eliminar).</summary>
    public const string Moderador = "Moderador";

    /// <summary>Aprueba/rechaza la verificación de identidad (KYC).</summary>
    public const string AdminKyc = "AdminKyc";

    /// <summary>Acceso total / gestión de la plataforma.</summary>
    public const string AdminPlataforma = "AdminPlataforma";

    /// <summary>Atención al cliente / tickets. Definido pero no operativo en v1.</summary>
    public const string Soporte = "Soporte";

    /// <summary>Actor de servicio (machine user) para automatización futura. No operativo en v1.</summary>
    public const string Sistema = "Sistema";

    /// <summary>Todos los roles definidos, en orden de seeding.</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        Cliente, Moderador, AdminKyc, AdminPlataforma, Soporte, Sistema,
    ];
}
