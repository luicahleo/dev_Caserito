namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>
/// Mapa autoritativo rol → permisos. Es la única fuente de verdad usada en tiempo de ejecución
/// para agregar los permisos de un usuario a partir de sus roles. Cambiar los permisos de un rol
/// solo requiere editar este mapa (no reescribir lógica de autorización).
/// </summary>
public static class MapaRolesPermisos
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _mapa =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [RolesApp.Cliente] = [],
            [RolesApp.Moderador] = [Permisos.PublicacionesModerar, Permisos.ChatModerar],
            [RolesApp.AdminKyc] = [Permisos.KycRevisar],
            [RolesApp.AdminPlataforma] = Permisos.Todos,
            [RolesApp.Soporte] = [Permisos.SoporteTickets],
            [RolesApp.Sistema] = [],
        };

    /// <summary>Permisos concedidos a un rol individual (vacío si el rol es desconocido).</summary>
    public static IReadOnlyList<string> PermisosDeRol(string rol) =>
        _mapa.TryGetValue(rol, out var permisos) ? permisos : [];

    /// <summary>Unión distinta de los permisos de todos los roles indicados.</summary>
    public static IReadOnlyCollection<string> PermisosDe(IEnumerable<string> roles) =>
        roles.SelectMany(PermisosDeRol).Distinct(StringComparer.Ordinal).ToArray();
}
