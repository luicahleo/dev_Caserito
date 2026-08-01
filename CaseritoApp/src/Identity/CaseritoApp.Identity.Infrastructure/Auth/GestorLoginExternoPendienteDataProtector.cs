using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <inheritdoc cref="IGestorLoginExternoPendiente"/>
public sealed class GestorLoginExternoPendienteDataProtector(
    IDataProtectionProvider proveedorProteccion,
    TimeProvider reloj) : IGestorLoginExternoPendiente
{
    public const string NombreCookie = "loginExternoPendiente";
    private static readonly TimeSpan _vigenciaMaxima = TimeSpan.FromMinutes(10);
    private readonly IDataProtector _protector = proveedorProteccion.CreateProtector(
        "CaseritoApp.LoginExternoPendiente.v1");

    /// <inheritdoc/>
    public string Proteger(LoginExternoPendiente pendiente)
    {
        var normalizado = ValidarYNormalizar(pendiente);
        return _protector.Protect(JsonSerializer.Serialize(normalizado));
    }

    /// <inheritdoc/>
    public bool TryDesproteger(string ticket, out LoginExternoPendiente? pendiente)
    {
        pendiente = null;
        try
        {
            var json = _protector.Unprotect(ticket);
            var desprotegido = JsonSerializer.Deserialize<LoginExternoPendiente>(json);
            if (desprotegido is null)
            {
                return false;
            }

            var normalizado = ValidarYNormalizar(desprotegido);
            if (normalizado.ExpiraEn <= reloj.GetUtcNow())
            {
                return false;
            }

            pendiente = normalizado;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void GuardarCookie(HttpResponse respuesta, LoginExternoPendiente pendiente, bool segura)
    {
        respuesta.Cookies.Append(NombreCookie, Proteger(pendiente), CrearOpcionesCookie(segura));
    }

    /// <inheritdoc/>
    public bool TryLeerCookie(HttpRequest solicitud, out LoginExternoPendiente? pendiente)
    {
        pendiente = null;
        return solicitud.Cookies.TryGetValue(NombreCookie, out var ticket) &&
            !string.IsNullOrWhiteSpace(ticket) &&
            TryDesproteger(ticket, out pendiente);
    }

    /// <inheritdoc/>
    public void BorrarCookie(HttpResponse respuesta, bool segura) =>
        respuesta.Cookies.Delete(NombreCookie, CrearOpcionesCookie(segura));

    /// <inheritdoc/>
    public LoginExternoPendienteProyeccion Proyectar(
        LoginExternoPendiente pendiente,
        bool requiereVinculacion) =>
        new(
            RequiereEmail: string.IsNullOrWhiteSpace(pendiente.Email),
            RequiereNombre: string.IsNullOrWhiteSpace(pendiente.Nombre),
            RequiereCiudad: true,
            RequiereVinculacion: requiereVinculacion,
            NombreVisible: string.IsNullOrWhiteSpace(pendiente.Nombre) ? null : pendiente.Nombre);

    private LoginExternoPendiente ValidarYNormalizar(LoginExternoPendiente pendiente)
    {
        var proveedor = pendiente.Proveedor.Trim().ToLowerInvariant();
        if (proveedor is not ("google" or "facebook"))
        {
            throw new ArgumentException("Proveedor externo no válido.", nameof(pendiente));
        }

        if (!EsRetornoLocal(pendiente.Retorno))
        {
            throw new ArgumentException("Ruta de retorno no válida.", nameof(pendiente));
        }

        var ahora = reloj.GetUtcNow();
        if (pendiente.ExpiraEn <= ahora || pendiente.ExpiraEn > ahora.Add(_vigenciaMaxima))
        {
            throw new ArgumentException("Vigencia del login externo no válida.", nameof(pendiente));
        }

        return pendiente with
        {
            Proveedor = proveedor,
            EmailConfiable = proveedor == "google" && pendiente.EmailConfiable,
        };
    }

    private static bool EsRetornoLocal(string retorno) =>
        !string.IsNullOrWhiteSpace(retorno) &&
        retorno[0] == '/' &&
        (retorno.Length == 1 || (retorno[1] != '/' && retorno[1] != '\\'));

    private static CookieOptions CrearOpcionesCookie(bool segura) => new()
    {
        HttpOnly = true,
        Secure = segura,
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth/external",
        MaxAge = _vigenciaMaxima,
        IsEssential = true,
    };
}
