namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Usuario con sus roles asignados, para la vista de administración. El email no se loguea.</summary>
public sealed record UsuarioConRolesDto(Guid Id, string Email, string Nombre, string Ciudad, IReadOnlyList<string> Roles);
