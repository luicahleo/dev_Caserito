using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Contrato de registro de un nuevo usuario.</summary>
public sealed record RegistroRequest(string Email, string Password, string Nombre, string Ciudad);

/// <summary>Contrato de inicio de sesión.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Respuesta con el access token JWT emitido.</summary>
public sealed record TokenAccesoResponse(string AccessToken);

/// <summary>Registro y mapeo del grupo minimal API <c>/api/auth</c>: register, login, refresh y logout.</summary>
public static class AuthEndpoints
{
    private const string NombreCookieRefresh = "refreshToken";

    /// <summary>Mapea los endpoints de autenticación bajo el prefijo <c>/api/auth</c>.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth");

        grupo.MapPost("/register", RegistrarAsync);
        grupo.MapPost("/login", LoginAsync);
        grupo.MapPost("/refresh", RefreshAsync);
        grupo.MapPost("/logout", LogoutAsync);

        return app;
    }

    private static async Task<IResult> RegistrarAsync(
        RegistroRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var usuario = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Nombre = request.Nombre,
            Ciudad = request.Ciudad,
        };

        var resultado = await userManager.CreateAsync(usuario, request.Password);

        if (!resultado.Succeeded)
        {
            return Results.ValidationProblem(
                resultado.Errors.ToDictionary(
                    e => e.Code,
                    e => new[] { e.Description }));
        }

        // Cada usuario nuevo recibe el rol por defecto Cliente (sembrado en el arranque).
        var resultadoRol = await userManager.AddToRoleAsync(usuario, RolesApp.Cliente);

        if (!resultadoRol.Succeeded)
        {
            return Results.ValidationProblem(
                resultadoRol.Errors.ToDictionary(
                    e => e.Code,
                    e => new[] { e.Description }));
        }

        return Results.Ok();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IGeneradorTokensAcceso generadorTokens,
        IServicioRefreshTokens servicioRefreshTokens,
        HttpContext contexto,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email);
        if (usuario is null)
        {
            return Results.Unauthorized();
        }

        var resultado = await signInManager.CheckPasswordSignInAsync(usuario, request.Password, lockoutOnFailure: true);
        if (!resultado.Succeeded)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var accessToken = generadorTokens.Generar(usuario, permisos);
        var refreshTokenPlano = await servicioRefreshTokens.EmitirAsync(usuario.Id, ct);

        EstablecerCookieRefresh(contexto, refreshTokenPlano, entorno);

        return Results.Ok(new TokenAccesoResponse(accessToken));
    }

    private static async Task<IResult> RefreshAsync(
        UserManager<ApplicationUser> userManager,
        IGeneradorTokensAcceso generadorTokens,
        IServicioRefreshTokens servicioRefreshTokens,
        HttpContext contexto,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        if (!contexto.Request.Cookies.TryGetValue(NombreCookieRefresh, out var tokenPlano) ||
            string.IsNullOrWhiteSpace(tokenPlano))
        {
            BorrarCookieRefresh(contexto, entorno);
            return Results.Unauthorized();
        }

        var rotado = await servicioRefreshTokens.RotarAsync(tokenPlano, ct);
        if (rotado is null)
        {
            BorrarCookieRefresh(contexto, entorno);
            return Results.Unauthorized();
        }

        var (nuevoTokenPlano, userId) = rotado.Value;
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            BorrarCookieRefresh(contexto, entorno);
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var accessToken = generadorTokens.Generar(usuario, permisos);
        EstablecerCookieRefresh(contexto, nuevoTokenPlano, entorno);

        return Results.Ok(new TokenAccesoResponse(accessToken));
    }

    private static async Task<IResult> LogoutAsync(
        IServicioRefreshTokens servicioRefreshTokens,
        HttpContext contexto,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        if (contexto.Request.Cookies.TryGetValue(NombreCookieRefresh, out var tokenPlano) &&
            !string.IsNullOrWhiteSpace(tokenPlano))
        {
            await servicioRefreshTokens.RevocarAsync(tokenPlano, ct);
        }

        BorrarCookieRefresh(contexto, entorno);

        return Results.NoContent();
    }

    private static void EstablecerCookieRefresh(HttpContext contexto, string tokenPlano, IHostEnvironment entorno)
    {
        contexto.Response.Cookies.Append(NombreCookieRefresh, tokenPlano, OpcionesCookieRefresh(entorno));
    }

    private static void BorrarCookieRefresh(HttpContext contexto, IHostEnvironment entorno)
    {
        contexto.Response.Cookies.Delete(NombreCookieRefresh, OpcionesCookieRefresh(entorno));
    }

    private static CookieOptions OpcionesCookieRefresh(IHostEnvironment entorno) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = !(entorno.IsDevelopment() || entorno.IsEnvironment("Testing")),
        Path = "/api/auth",
    };
}
