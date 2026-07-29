using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Auth;

/// <summary>
/// Puerto que permite a la capa de aplicación confirmar el email de un usuario sin depender
/// directamente de ASP.NET Core Identity.
/// </summary>
public interface IRepositorioConfirmacionEmail
{
    /// <summary>
    /// Marca el email del usuario como confirmado. Si ya lo estaba, devuelve éxito silencioso.
    /// </summary>
    public Task<Result> ConfirmarEmailAsync(Guid userId, CancellationToken cancellationToken);
}
