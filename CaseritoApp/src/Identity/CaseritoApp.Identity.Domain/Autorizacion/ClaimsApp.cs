namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Tipos de claim propios de la aplicación emitidos en el JWT.</summary>
public static class ClaimsApp
{
    /// <summary>Claim de permiso concedido (uno por permiso). No es PII.</summary>
    public const string Permiso = "perm";

    /// <summary>Tipo de claim que indica que el usuario tiene una verificación KYC aprobada.</summary>
    public const string Verificado = "verificado";
}
