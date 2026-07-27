using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Notifications.Application.Notificaciones;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Host.Notifications;

public sealed class ConsultaEmailUsuarioAdapter(
    UserManager<ApplicationUser> userManager) : IConsultaEmailUsuario
{
    public async Task<string?> ObtenerEmailAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        return usuario?.Email;
    }
}
