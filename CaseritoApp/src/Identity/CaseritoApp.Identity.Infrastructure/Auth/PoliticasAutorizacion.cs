namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Nombres de policies de autorización basadas en permiso.</summary>
public static class PoliticasAutorizacion
{
    /// <summary>Nombre de la policy que exige el claim <c>perm</c> con el permiso indicado.</summary>
    public static string Permiso(string permiso) => $"perm:{permiso}";
}
