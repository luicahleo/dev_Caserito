using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Datos necesarios para enviar un enlace de restablecimiento.</summary>
public sealed record SolicitudRestablecimiento(
    Guid UsuarioId,
    string Email,
    string Nombre,
    string Token);

/// <summary>
/// Puerto para generar y consumir tokens de restablecimiento sin exponer ASP.NET Core Identity
/// a la capa de aplicación.
/// </summary>
public interface IRepositorioRestablecimientoPassword
{
    /// <summary>
    /// Crea una solicitud si existe una cuenta con correo utilizable; en caso contrario devuelve
    /// <c>null</c> para que el caso de uso mantenga una respuesta uniforme.
    /// </summary>
    public Task<SolicitudRestablecimiento?> CrearSolicitudAsync(
        string email,
        CancellationToken cancellationToken);

    /// <summary>Restablece la contraseña si usuario y token son válidos.</summary>
    public Task<Result<Guid>> RestablecerAsync(
        Guid usuarioId,
        string token,
        string password,
        CancellationToken cancellationToken);
}
