using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>
/// Datos de perfil expuestos por el contexto de Identity: identidad, email y los campos
/// editables (nombre, ciudad) del usuario.
/// </summary>
public sealed record PerfilDto(
    Guid Id,
    string Email,
    string Nombres,
    string Apellidos,
    Guid CiudadId,
    string NombreCiudad,
    bool Verificado);

/// <summary>
/// Abstracción (puerto) que permite a la capa de aplicación leer y actualizar el perfil de un
/// usuario sin depender directamente de ASP.NET Core Identity. La implementación (adaptador)
/// vive en <c>CaseritoApp.Identity.Infrastructure</c>, apoyada en <c>UserManager&lt;ApplicationUser&gt;</c>.
/// </summary>
public interface IRepositorioPerfil
{
    /// <summary>Obtiene el perfil del usuario indicado, o <c>null</c> si no existe.</summary>
    public Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Obtiene únicamente los datos aptos para exposición pública.</summary>
    public Task<PerfilPublicoDto?> ObtenerPublicoAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Actualiza la identidad y ciudad del usuario indicado.</summary>
    public Task<Result> ActualizarAsync(
        Guid userId,
        string nombres,
        string apellidos,
        Guid ciudadId,
        CancellationToken cancellationToken);
}
