using System.Text;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.Identity.Infrastructure.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Correo;
using CaseritoApp.Identity.Infrastructure.Kyc;
using CaseritoApp.Identity.Infrastructure.Perfil;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Registro de ASP.NET Core Identity y su persistencia para el contexto de Identity.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el <see cref="IdentityDbContext"/> (si hay cadena de conexión configurada) junto con
    /// Identity Core, roles, stores de EF, <see cref="SignInManager{TUser}"/> y los proveedores de token
    /// por defecto.
    /// </summary>
    public static IServiceCollection AgregarIdentity(
        this IServiceCollection servicios, IConfiguration config, IHostEnvironment entorno)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(cadena));
        }

        servicios
            .AddIdentityCore<ApplicationUser>(opciones =>
            {
                opciones.Password.RequiredLength = 8;
                opciones.User.RequireUniqueEmail = true;
                opciones.Tokens.PasswordResetTokenProvider =
                    ProveedorTokenRestablecimientoPassword.Nombre;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<ProveedorTokenRestablecimientoPassword>(
                ProveedorTokenRestablecimientoPassword.Nombre);

        servicios.AddScoped<IRepositorioPerfil, RepositorioPerfilUserManager>();
        servicios.AddScoped<IRepositorioConfirmacionEmail, RepositorioConfirmacionEmail>();
        servicios.AddScoped<IRepositorioRestablecimientoPassword, RepositorioRestablecimientoPassword>();
        servicios.AddScoped<IRepositorioRolesUsuario, RepositorioRolesUsuarioUserManager>();
        servicios.AddScoped<IRepositorioVerificacionKyc, RepositorioVerificacionKycEfCore>();
        servicios.AddScoped<IConsultaVerificacionKyc, ConsultaVerificacionKycEfCore>();
        servicios.Configure<OpcionesAlmacenKyc>(config.GetSection(OpcionesAlmacenKyc.Seccion));
        servicios.Configure<OpcionesArgos>(config.GetSection(OpcionesArgos.Seccion));
        servicios.Configure<OpcionesCorreo>(config.GetSection(OpcionesCorreo.Seccion));
        servicios.AddSingleton(
            config.GetSection(OpcionesApp.Seccion).Get<OpcionesApp>() ?? new OpcionesApp());
        servicios.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
        servicios.AddScoped<IPlantillaCorreo, PlantillaCorreoTextoPlano>();
        servicios.AddSingleton<IGeneradorTokenEmail>(sp =>
        {
            var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();
            var reloj = sp.GetRequiredService<TimeProvider>();
            return new GeneradorTokenEmailDataProtector(dataProtection, reloj);
        });

        var permiteArgosOpcional = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");
        servicios.AddOptions<OpcionesArgos>()
            .Validate(
                o => permiteArgosOpcional || !string.IsNullOrWhiteSpace(o.Url),
                "Argos:Url es obligatorio fuera de Development/Testing")
            .ValidateOnStart();

        servicios.AddHttpClient<IVerificadorIdentidadArgos, VerificadorIdentidadArgosHttp>();

        // Cifrado de PII: en Development/Testing se mantiene el Passthrough (sin cifrado, cómodo
        // para depurar y tests). Fuera de esos entornos se cablea el encryptor real sobre
        // ASP.NET Core Data Protection, con el key ring persistido en disco (volumen del host)
        // para que sobreviva al recreate del contenedor. Mismo criterio de entorno que la clave
        // efímera de JWT.
        if (entorno.IsDevelopment() || entorno.IsEnvironment("Testing"))
        {
            servicios.AddSingleton<IEncryptor, PassthroughEncryptor>();
        }
        else
        {
            var rutaClaves = config.GetValue<string>("DataProtection:RutaClaves")
                ?? "/data/dataprotection-keys";
            servicios.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
            servicios.AddSingleton<IEncryptor, DataProtectionEncryptor>();
        }
        servicios.AddScoped<IAlmacenBlobsKyc, AlmacenBlobsKycDisco>();
        servicios.AddSingleton<IPublicadorEventosIntegracion, PublicadorEventosIntegracionLog>();
        servicios.AddSingleton<IAuditorAccesoPii, AuditorAccesoPiiLog>();

        // Registro incondicional (no atado a la presencia de cadena de conexión): en tests de
        // integración (CaseritoApiFactory) el IdentityDbContext se registra por fuera de este
        // método (RemoveAll + AddDbContext contra Testcontainers), así que atar este registro al
        // mismo "if" de arriba dejaría IUnitOfWork sin resolver ahí. Solo falla en tiempo de
        // resolución (al manejar un ICommand/IQuery de Identity) si IdentityDbContext no está
        // registrado, igual que ya ocurre con UserManager/SignInManager en ese escenario.
        servicios.AddScoped<IUnitOfWork, UnitOfWorkIdentity>();

        return servicios;
    }

    /// <summary>
    /// Registra la autenticación JWT Bearer: bindea <see cref="OpcionesJwt"/> desde la sección
    /// <c>Jwt</c>, registra <see cref="TimeProvider"/> y <see cref="IGeneradorTokensAcceso"/>, y
    /// configura <c>AddJwtBearer</c> con los parámetros de validación del token. También registra
    /// <c>AddAuthorization</c>.
    /// </summary>
    public static IServiceCollection AgregarAutenticacionJwt(
        this IServiceCollection servicios, IConfiguration config, IHostEnvironment entorno)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton<ProveedorClaveFirma>();
        servicios.AddSingleton<IGeneradorTokensAcceso, GeneradorTokensAcceso>();
        servicios.AddScoped<IEmisorSesion, EmisorSesion>();
        servicios.AddScoped<ServicioRefreshTokens>();
        servicios.AddScoped<IServicioRefreshTokens>(
            proveedor => proveedor.GetRequiredService<ServicioRefreshTokens>());
        servicios.AddScoped<IRevocadorSesionesUsuario>(
            proveedor => proveedor.GetRequiredService<ServicioRefreshTokens>());

        // Solo en Development/Testing se admite una clave efímera de repuesto; fuera de esos
        // entornos la ausencia de "Jwt:Key" es un error de configuración crítico y debe fallar
        // rápido (fail-fast). ValidateOnStart corre al construir el host, así que un despliegue mal
        // configurado ni siquiera arranca.
        var permiteClaveEfimera = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");

        servicios.AddOptions<OpcionesJwt>()
            .Bind(config.GetSection(OpcionesJwt.Seccion))
            .Validate(
                o => permiteClaveEfimera
                    || (!string.IsNullOrWhiteSpace(o.Key) && Encoding.UTF8.GetByteCount(o.Key) >= 32),
                "Jwt:Key es obligatorio y debe tener al menos 32 bytes fuera de Development/Testing")
            .ValidateOnStart();

        var opcionesExternas = config
            .GetSection(OpcionesAutenticacionExterna.Seccion)
            .Get<OpcionesAutenticacionExterna>() ?? new OpcionesAutenticacionExterna();
        servicios.AddSingleton(opcionesExternas);

        var permiteProveedoresDeshabilitados = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");
        servicios.AddOptions<OpcionesAutenticacionExterna>()
            .Bind(config.GetSection(OpcionesAutenticacionExterna.Seccion))
            .Validate(
                o => permiteProveedoresDeshabilitados || (o.Google.Habilitado && o.Facebook.Habilitado),
                "Las credenciales de Google y Facebook son obligatorias fuera de Development/Testing")
            .ValidateOnStart();

        var autenticacion = servicios
            .AddAuthentication(opciones =>
            {
                opciones.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opciones.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddCookie(IdentityConstants.ExternalScheme, opciones =>
            {
                opciones.Cookie.HttpOnly = true;
                opciones.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                opciones.Cookie.SecurePolicy = permiteProveedoresDeshabilitados
                    ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                    : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                opciones.Cookie.Path = "/api/auth/external";
                opciones.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                opciones.SlidingExpiration = false;
            });

        if (opcionesExternas.Google.Habilitado)
        {
            autenticacion.AddGoogle(GoogleDefaults.AuthenticationScheme, opciones =>
            {
                opciones.ClientId = opcionesExternas.Google.ClientId;
                opciones.ClientSecret = opcionesExternas.Google.ClientSecret;
                opciones.SignInScheme = IdentityConstants.ExternalScheme;
                opciones.CallbackPath = "/api/auth/external/google/callback";
            });
        }

        if (opcionesExternas.Facebook.Habilitado)
        {
            autenticacion.AddFacebook(FacebookDefaults.AuthenticationScheme, opciones =>
            {
                opciones.AppId = opcionesExternas.Facebook.AppId;
                opciones.AppSecret = opcionesExternas.Facebook.AppSecret;
                opciones.SignInScheme = IdentityConstants.ExternalScheme;
                opciones.CallbackPath = "/api/auth/external/facebook/callback";
            });
        }

        // La configuración de JwtBearerOptions se hace vía IConfigureNamedOptions para inyectar el
        // OpcionesJwt bindeado y la clave compartida (ProveedorClaveFirma), en vez de releer la
        // config a mano. Así firma (GeneradorTokensAcceso) y validación usan la misma clave.
        servicios.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigurarJwtBearer>();

        servicios.AddAuthorization(opciones =>
        {
            // Una policy por permiso: exige el claim "perm" con ese valor. La autorización chequea
            // permisos, no roles (los roles solo agregan permisos al emitir el token).
            foreach (var permiso in Permisos.Todos)
            {
                opciones.AddPolicy(
                    PoliticasAutorizacion.Permiso(permiso),
                    p => p.RequireClaim(ClaimsApp.Permiso, permiso));
            }
        });

        return servicios;
    }
}
