using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Usuario de la aplicación (ASP.NET Core Identity) con datos de perfil.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public Guid CiudadId { get; set; }

    // Compatibilidad temporal durante la migración expand/contract de los consumidores.
}
