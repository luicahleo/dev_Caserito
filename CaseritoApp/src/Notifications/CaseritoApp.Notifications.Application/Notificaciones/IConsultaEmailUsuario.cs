namespace CaseritoApp.Notifications.Application.Notificaciones;

/// <summary>
/// Puerto de aplicación para resolver la dirección de correo de un usuario.
/// </summary>
public interface IConsultaEmailUsuario
{
    public Task<string?> ObtenerEmailAsync(Guid usuarioId, CancellationToken ct);
}
