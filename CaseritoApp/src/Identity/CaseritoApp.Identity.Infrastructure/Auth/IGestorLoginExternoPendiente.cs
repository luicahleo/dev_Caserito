using Microsoft.AspNetCore.Http;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Protege y conserva temporalmente un login externo pendiente.</summary>
public interface IGestorLoginExternoPendiente
{
    public string Proteger(LoginExternoPendiente pendiente);

    public bool TryDesproteger(string ticket, out LoginExternoPendiente? pendiente);

    public void GuardarCookie(HttpResponse respuesta, LoginExternoPendiente pendiente, bool segura);

    public bool TryLeerCookie(HttpRequest solicitud, out LoginExternoPendiente? pendiente);

    public void BorrarCookie(HttpResponse respuesta, bool segura);

    public LoginExternoPendienteProyeccion Proyectar(
        LoginExternoPendiente pendiente,
        bool requiereVinculacion);
}
