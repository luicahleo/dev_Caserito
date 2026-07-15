namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Códigos de error de la gestión de roles, compartidos entre handlers y el mapeo HTTP del endpoint.</summary>
public static class CodigosErrorRoles
{
    /// <summary>El usuario objetivo no existe → HTTP 404.</summary>
    public const string UsuarioNoEncontrado = "Roles.UsuarioNoEncontrado";

    /// <summary>Se intentó quitar AdminPlataforma al último que lo tiene → HTTP 409.</summary>
    public const string UltimoAdminPlataforma = "Roles.UltimoAdminPlataforma";
}
