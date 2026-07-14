using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Usuario de la aplicación (ASP.NET Core Identity) con datos de perfil.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Nombre { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
}
