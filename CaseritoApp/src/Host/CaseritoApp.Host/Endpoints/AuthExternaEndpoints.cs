using System.Security.Claims;
using System.Text.Json.Serialization;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints de navegación para autenticación externa.</summary>
public static class AuthExternaEndpoints
{
    private const string RetornoPredeterminado = "/perfil";

    public static IEndpointRouteBuilder MapAuthExternaEndpoints(this IEndpointRouteBuilder app)
    {
        var entorno = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var opciones = app.ServiceProvider.GetRequiredService<OpcionesAutenticacionExterna>();
        if (opciones.Simulador.Habilitado && !entorno.IsDevelopment())
        {
            throw new InvalidOperationException(
                "El simulador de autenticación externa solo puede habilitarse en Development.");
        }

        var grupo = app.MapGroup("/api/auth/external");
        grupo.MapGet("/providers", ObtenerProveedores)
            .WithName("ObtenerProveedoresExternos")
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK);
        grupo.MapGet("/{provider}/start", IniciarAsync)
            .ExcludeFromDescription();
        grupo.MapGet("/callback", CallbackAsync)
            .ExcludeFromDescription();
        grupo.MapPost("/simulator/authorize", AutorizarSimuladorAsync)
            .DisableAntiforgery()
            .ExcludeFromDescription();
        grupo.MapGet("/pending", ObtenerPendienteAsync)
            .WithName("ObtenerLoginExternoPendiente")
            .Produces<LoginExternoPendienteProyeccion>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        grupo.MapPost("/complete", CompletarAsync)
            .WithName("CompletarLoginExterno")
            .Accepts<CompletarRegistroExternoRequest>("application/json")
            .Produces<TokenAccesoResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);
        grupo.MapPost("/link", VincularAsync)
            .WithName("VincularLoginExterno")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        return app;
    }

    private static async Task<IResult> AutorizarSimuladorAsync(
        HttpContext contexto,
        OpcionesAutenticacionExterna opciones,
        IHostEnvironment entorno)
    {
        if (!entorno.IsDevelopment() || !opciones.Simulador.Habilitado)
        {
            return Results.NotFound();
        }

        var formulario = await contexto.Request.ReadFormAsync(contexto.RequestAborted);
        var proveedor = formulario["provider"].ToString().ToLowerInvariant();
        var escenario = formulario["scenario"].ToString().ToLowerInvariant();
        var retorno = NormalizarRetorno(formulario["returnUrl"].ToString());
        if (proveedor is not ("google" or "facebook") ||
            escenario is not ("nuevo" or "reutilizable" or "vinculacion"))
        {
            return Results.BadRequest();
        }

        if (string.Equals(formulario["action"], "cancelar", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Redirect("/login?authExterna=cancelado");
        }

        var perfil = CrearPerfilSimulado(proveedor, escenario, opciones);
        var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, perfil.Clave),
                new Claim(ClaimTypes.Email, perfil.Email),
                new Claim(ClaimTypes.Name, perfil.Nombre),
                new Claim("email_verified", perfil.EmailVerificado ? "true" : "false"),
            ],
            IdentityConstants.ExternalScheme);
        var propiedades = new AuthenticationProperties();
        propiedades.Items["LoginProvider"] = proveedor;
        await contexto.SignInAsync(
            IdentityConstants.ExternalScheme,
            new ClaimsPrincipal(identidad),
            propiedades);

        return Results.Redirect(
            $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(retorno)}");
    }

    private static PerfilSimulado CrearPerfilSimulado(
        string proveedor,
        string escenario,
        OpcionesAutenticacionExterna opciones)
    {
        var email = escenario == "vinculacion"
            ? opciones.Simulador.CorreoExistente
            : $"{escenario}.{proveedor}@caserito.test";
        return new(
            $"simulador:{proveedor}:{escenario}",
            email,
            $"Usuario de prueba {proveedor}",
            proveedor == "google");
    }

    private static IResult ObtenerProveedores(
        OpcionesAutenticacionExterna opciones,
        IHostEnvironment entorno)
    {
        var proveedores = new List<string>(2);
        var simulador = entorno.IsDevelopment() && opciones.Simulador.Habilitado;
        if (simulador || opciones.Facebook.Habilitado)
        {
            proveedores.Add("facebook");
        }

        if (simulador || opciones.Google.Habilitado)
        {
            proveedores.Add("google");
        }
        return Results.Ok(proveedores);
    }

    private sealed record PerfilSimulado(
        string Clave,
        string Email,
        string Nombre,
        bool EmailVerificado);

    private static IResult IniciarAsync(
        string provider,
        string? returnUrl,
        SignInManager<ApplicationUser> signInManager,
        OpcionesAutenticacionExterna opciones,
        IHostEnvironment entorno)
    {
        var proveedor = provider.ToLowerInvariant();
        if (entorno.IsDevelopment() && opciones.Simulador.Habilitado &&
            proveedor is "google" or "facebook")
        {
            var retornoSimulado = Uri.EscapeDataString(NormalizarRetorno(returnUrl));
            return Results.Redirect(
                $"/auth/external/simulador?provider={proveedor}&returnUrl={retornoSimulado}");
        }

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

        var usuario = await usuarios.FindByLoginAsync(proveedor, clave);
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
        var retornoOnboarding = Uri.EscapeDataString(NormalizarRetorno(returnUrl));
        return Results.Redirect($"/auth/external/onboarding?returnUrl={retornoOnboarding}");
    }

    private static async Task<IResult> ObtenerPendienteAsync(
        HttpContext contexto,
        IGestorLoginExternoPendiente gestor,
        UserManager<ApplicationUser> usuarios)
    {
        if (!gestor.TryLeerCookie(contexto.Request, out var pendiente) || pendiente is null)
        {
            return Results.Unauthorized();
        }

        var requiereVinculacion = !string.IsNullOrWhiteSpace(pendiente.Email) &&
            await usuarios.FindByEmailAsync(pendiente.Email) is not null;
        return Results.Ok(gestor.Proyectar(pendiente, requiereVinculacion));
    }

    private static async Task<IResult> CompletarAsync(
        CompletarRegistroExternoRequest request,
        HttpContext contexto,
        IGestorLoginExternoPendiente gestor,
        ServicioRegistroExterno servicio,
        IConsultaCiudadesPerfil consultaCiudades,
        IEmisorSesion emisorSesion,
        IHostEnvironment entorno,
        CancellationToken ct)
    {
        if (!gestor.TryLeerCookie(contexto.Request, out var pendiente) || pendiente is null)
        {
            return Results.Unauthorized();
        }

        var email = pendiente.Email ?? request.Email;
        var errores = ValidarDatos(email, request.Nombres, request.Apellidos, request.CiudadId);
        if (request.CiudadId is { } ciudadId &&
            !await consultaCiudades.ExisteActivaAsync(ciudadId, ct))
        {
            errores["ciudadId"] = ["Selecciona una ciudad válida."];
        }
        if (errores.Count > 0)
        {
            return Results.ValidationProblem(errores);
        }

        var resultado = await servicio.RegistrarAsync(
            pendiente,
            email!,
            request.Nombres!.Trim(),
            request.Apellidos!.Trim(),
            request.CiudadId!.Value,
            ct);
        if (resultado.Estado == EstadoRegistroExterno.RequiereVinculacion)
        {
            return Results.Conflict(new { mensaje = "Se requiere verificar la cuenta existente." });
        }

        if (resultado.Usuario is null)
        {
            gestor.BorrarCookie(contexto.Response, EsCookieSegura(entorno));
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "No se pudo completar el acceso externo.");
        }

        var sesion = await emisorSesion.EmitirAsync(resultado.Usuario, ct);
        AuthEndpoints.EstablecerCookieRefresh(contexto, sesion.RefreshToken, entorno);
        gestor.BorrarCookie(contexto.Response, EsCookieSegura(entorno));
        return Results.Ok(new TokenAccesoResponse(sesion.AccessToken));
    }

    private static Dictionary<string, string[]> ValidarDatos(
        string? email,
        string? nombres,
        string? apellidos,
        Guid? ciudadId)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            errores["email"] = ["El email no es válido."];
        }

        if (string.IsNullOrWhiteSpace(nombres) || nombres.Length > 100)
        {
            errores["nombres"] = ["Los nombres son obligatorios y admiten hasta 100 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(apellidos) || apellidos.Length > 100)
        {
            errores["apellidos"] = ["Los apellidos son obligatorios y admiten hasta 100 caracteres."];
        }

        if (ciudadId is null || ciudadId == Guid.Empty)
        {
            errores["ciudadId"] = ["La ciudad es obligatoria."];
        }

        return errores;
    }

    private static async Task<IResult> VincularAsync(
        ClaimsPrincipal principal,
        HttpContext contexto,
        IGestorLoginExternoPendiente gestor,
        ServicioRegistroExterno servicio,
        IHostEnvironment entorno)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(sub, out var usuarioId) ||
            !gestor.TryLeerCookie(contexto.Request, out var pendiente) || pendiente is null)
        {
            gestor.BorrarCookie(contexto.Response, EsCookieSegura(entorno));
            return Results.Unauthorized();
        }

        var vinculado = await servicio.VincularAsync(pendiente, usuarioId);
        gestor.BorrarCookie(contexto.Response, EsCookieSegura(entorno));
        return vinculado ? Results.NoContent() : Results.BadRequest();
    }

    private static bool EsCookieSegura(IHostEnvironment entorno) =>
        !(entorno.IsDevelopment() || entorno.IsEnvironment("Testing"));

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

/// <summary>Datos que faltan para completar un alta desde un proveedor externo.</summary>
[method: JsonConstructor]
public sealed record CompletarRegistroExternoRequest(
    string? Email,
    string? Nombres,
    string? Apellidos,
    Guid? CiudadId)
{
    // Compatibilidad transitoria para pruebas internas durante la migración expand/contract.
    public CompletarRegistroExternoRequest(string? email, string? ciudad, string? nombres)
        : this(email, nombres ?? "Nombre", "Prueba", new Guid("22222222-2222-2222-2222-000000000001"))
    {
    }
}
