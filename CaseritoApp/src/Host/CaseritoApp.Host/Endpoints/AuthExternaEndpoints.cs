using System.Security.Claims;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints de navegación para autenticación externa.</summary>
public static class AuthExternaEndpoints
{
    private const string RetornoPredeterminado = "/perfil";

    public static IEndpointRouteBuilder MapAuthExternaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth/external");
        grupo.MapGet("/providers", ObtenerProveedores);
        grupo.MapGet("/{provider}/start", IniciarAsync);
        grupo.MapGet("/callback", CallbackAsync);
        return app;
    }

    private static IResult ObtenerProveedores(OpcionesAutenticacionExterna opciones)
    {
        var proveedores = new List<string>(2);
        if (opciones.Facebook.Habilitado)
        {
            proveedores.Add("facebook");
        }

        if (opciones.Google.Habilitado)
        {
            proveedores.Add("google");
        }
        return Results.Ok(proveedores);
    }

    private static IResult IniciarAsync(
        string provider,
        string? returnUrl,
        SignInManager<ApplicationUser> signInManager,
        OpcionesAutenticacionExterna opciones)
    {
        var esquema = ObtenerEsquema(provider, opciones);
        if (esquema is null)
        {
            return Results.NotFound();
        }

        var retorno = NormalizarRetorno(returnUrl);
        var callback = $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(retorno)}";
        var propiedades = signInManager.ConfigureExternalAuthenticationProperties(esquema, callback);
        return Results.Challenge(propiedades, [esquema]);
    }

    private static async Task<IResult> CallbackAsync(
        string? returnUrl,
        HttpContext contexto,
        UserManager<ApplicationUser> usuarios,
        IEmisorSesion emisorSesion,
        IGestorLoginExternoPendiente gestorPendiente,
        TimeProvider tiempo,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        var autenticacion = await contexto.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!autenticacion.Succeeded || autenticacion.Principal is null)
        {
            await contexto.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect("/login?authExterna=cancelado");
        }

        string? proveedorEsquema = null;
        autenticacion.Properties?.Items.TryGetValue("LoginProvider", out proveedorEsquema);
        var proveedor = proveedorEsquema?.ToLowerInvariant();
        var clave = autenticacion.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (proveedor is not ("google" or "facebook") || string.IsNullOrWhiteSpace(clave))
        {
            await contexto.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect("/login?authExterna=fallo");
        }

        var usuario = await usuarios.FindByLoginAsync(proveedorEsquema!, clave);
        if (usuario is not null)
        {
            var sesion = await emisorSesion.EmitirAsync(usuario, ct);
            AuthEndpoints.EstablecerCookieRefresh(contexto, sesion.RefreshToken, entorno);
            await contexto.SignOutAsync(IdentityConstants.ExternalScheme);
            var retorno = Uri.EscapeDataString(NormalizarRetorno(returnUrl));
            return Results.Redirect($"/auth/external/completado?returnUrl={retorno}");
        }

        var email = autenticacion.Principal.FindFirstValue(ClaimTypes.Email);
        var nombre = autenticacion.Principal.FindFirstValue(ClaimTypes.Name);
        var verificado = proveedor == "google" &&
            string.Equals(autenticacion.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        gestorPendiente.GuardarCookie(
            contexto.Response,
            new LoginExternoPendiente(
                proveedor,
                clave,
                email,
                verificado,
                nombre,
                NormalizarRetorno(returnUrl),
                tiempo.GetUtcNow().AddMinutes(10)),
            segura: !(entorno.IsDevelopment() || entorno.IsEnvironment("Testing")));
        await contexto.SignOutAsync(IdentityConstants.ExternalScheme);
        return Results.Redirect("/auth/external/onboarding");
    }

    private static string? ObtenerEsquema(string provider, OpcionesAutenticacionExterna opciones) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "google" when opciones.Google.Habilitado => GoogleDefaults.AuthenticationScheme,
            "facebook" when opciones.Facebook.Habilitado => FacebookDefaults.AuthenticationScheme,
            _ => null,
        };

    private static string NormalizarRetorno(string? retorno) =>
        !string.IsNullOrWhiteSpace(retorno) && retorno[0] == '/' &&
        (retorno.Length == 1 || (retorno[1] != '/' && retorno[1] != '\\'))
            ? retorno
            : RetornoPredeterminado;
}
