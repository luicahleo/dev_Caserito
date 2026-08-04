using System.Security.Claims;
using System.Text.Json.Serialization;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Usuarios;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Contrato de registro de un nuevo usuario.</summary>
[method: JsonConstructor]
public sealed record RegistroRequest(
    string Email,
    string Password,
    string Nombres,
    string Apellidos,
    Guid CiudadId)
{
    // Compatibilidad transitoria para pruebas internas durante la migración expand/contract.
    public RegistroRequest(string email, string password, string nombres, string ciudad)
        : this(email, password, nombres, "Prueba", new Guid("22222222-2222-2222-2222-000000000001"))
    {
    }
}

/// <summary>Contrato de inicio de sesión.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Contrato de confirmación de email.</summary>
public sealed record ConfirmarEmailRequest(Guid UsuarioId, string Token);

/// <summary>Contrato de solicitud de restablecimiento de contraseña.</summary>
public sealed record OlvidePasswordRequest(string Email);

/// <summary>Contrato de consumo de un enlace de restablecimiento.</summary>
public sealed record RestablecerPasswordRequest(Guid UsuarioId, string Token, string Password);

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

        grupo.MapPost("/register", RegistrarAsync)
            .Accepts<RegistroRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        grupo.MapPost("/login", LoginAsync)
            .Accepts<LoginRequest>("application/json")
            .Produces<TokenAccesoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/refresh", RefreshAsync)
            .Produces<TokenAccesoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/logout", LogoutAsync)
            .Produces(StatusCodes.Status204NoContent);

        grupo.MapPost("/confirm-email", ConfirmarEmailAsync)
            .Accepts<ConfirmarEmailRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapPost("/resend-confirmation", ReenviarConfirmacionAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth-reenvio-confirmacion")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/forgot-password", SolicitarRestablecimientoPasswordAsync)
            .Accepts<OlvidePasswordRequest>("application/json")
            .RequireRateLimiting("auth-forgot-password")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/reset-password", RestablecerPasswordAsync)
            .Accepts<RestablecerPasswordRequest>("application/json")
            .RequireRateLimiting("auth-reset-password")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> SolicitarRestablecimientoPasswordAsync(
        OlvidePasswordRequest request,
        ISender sender,
        CancellationToken ct)
    {
        await sender.Send(new SolicitarRestablecimientoPasswordCommand(request.Email), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RestablecerPasswordAsync(
        RestablecerPasswordRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var resultado = await sender.Send(
            new RestablecerPasswordCommand(request.UsuarioId, request.Token, request.Password),
            ct);

        return resultado.EsExito
            ? Results.NoContent()
            : Results.Problem(
                title: resultado.Error.Code,
                detail: resultado.Error.Message,
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> RegistrarAsync(
        RegistroRequest request,
        UserManager<ApplicationUser> userManager,
        IConsultaCiudadesPerfil consultaCiudades,
        IPublisher publisher,
        TimeProvider tiempo,
        CancellationToken ct)
    {
        var errores = ValidarRegistro(request);
        if (!await consultaCiudades.ExisteActivaAsync(request.CiudadId, ct))
        {
            errores["ciudadId"] = ["Selecciona una ciudad válida."];
        }

        if (errores.Count > 0)
        {
            return Results.ValidationProblem(errores);
        }

        var usuario = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            CiudadId = request.CiudadId,
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

        await publisher.Publish(
            new UsuarioRegistrado(Guid.NewGuid(), tiempo.GetUtcNow(), usuario.Id, usuario.Email!, usuario.Nombres),
            ct);

        return Results.Ok();
    }

    private static Dictionary<string, string[]> ValidarRegistro(RegistroRequest request)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.Nombres) || request.Nombres.Length > 100)
        {
            errores["nombres"] = ["Los nombres son obligatorios y admiten hasta 100 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Apellidos) || request.Apellidos.Length > 100)
        {
            errores["apellidos"] = ["Los apellidos son obligatorios y admiten hasta 100 caracteres."];
        }

        if (request.CiudadId == Guid.Empty)
        {
            errores["ciudadId"] = ["La ciudad es obligatoria."];
        }

        return errores;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmisorSesion emisorSesion,
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

        var sesion = await emisorSesion.EmitirAsync(usuario, ct);

        EstablecerCookieRefresh(contexto, sesion.RefreshToken, entorno);

        return Results.Ok(new TokenAccesoResponse(sesion.AccessToken));
    }

    private static async Task<IResult> RefreshAsync(
        UserManager<ApplicationUser> userManager,
        IEmisorSesion emisorSesion,
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

        var (nuevoRefreshToken, userId) = rotado.Value;
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            BorrarCookieRefresh(contexto, entorno);
            return Results.Unauthorized();
        }

        var accessToken = await emisorSesion.EmitirTokenAccesoAsync(usuario, ct);
        EstablecerCookieRefresh(contexto, nuevoRefreshToken, entorno);

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

    private static async Task<IResult> ConfirmarEmailAsync(
        ConfirmarEmailRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var resultado = await sender.Send(
            new ConfirmarEmailCommand(request.UsuarioId, request.Token),
            ct);

        return resultado.EsExito
            ? Results.NoContent()
            : Results.Problem(
                title: resultado.Error.Code,
                detail: resultado.Error.Message,
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> ReenviarConfirmacionAsync(
        ClaimsPrincipal usuario,
        UserManager<ApplicationUser> userManager,
        IPublisher publisher,
        TimeProvider tiempo,
        CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser is null || appUser.EmailConfirmed)
        {
            return Results.NoContent();
        }

        await publisher.Publish(
            new UsuarioRegistrado(
                Guid.NewGuid(),
                tiempo.GetUtcNow(),
                appUser.Id,
                appUser.Email!,
                appUser.Nombres),
            ct);

        return Results.NoContent();
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(valor, out userId);
    }

    internal static void EstablecerCookieRefresh(HttpContext contexto, string tokenPlano, IHostEnvironment entorno)
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
