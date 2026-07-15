using System.Text;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.Identity.Infrastructure.Perfil;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    public static IServiceCollection AgregarIdentity(this IServiceCollection servicios, IConfiguration config)
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
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

        servicios.AddScoped<IRepositorioPerfil, RepositorioPerfilUserManager>();

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
        servicios.AddScoped<IServicioRefreshTokens, ServicioRefreshTokens>();

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

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // La configuración de JwtBearerOptions se hace vía IConfigureNamedOptions para inyectar el
        // OpcionesJwt bindeado y la clave compartida (ProveedorClaveFirma), en vez de releer la
        // config a mano. Así firma (GeneradorTokensAcceso) y validación usan la misma clave.
        servicios.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigurarJwtBearer>();

        servicios.AddAuthorization();

        return servicios;
    }
}
