namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Un rol del MVP y los permisos que agrega (derivado de <c>MapaRolesPermisos</c>).</summary>
public sealed record RolDto(string Rol, IReadOnlyList<string> Permisos);
