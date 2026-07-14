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
using Microsoft.IdentityModel.Tokens;

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
        servicios.AddSingleton<IGeneradorTokensAcceso, GeneradorTokensAcceso>();
        servicios.AddScoped<IServicioRefreshTokens, ServicioRefreshTokens>();

        // Solo en Development/Testing se admite una clave efímera de repuesto; fuera de esos
        // entornos la ausencia de "Jwt:Key" es un error de configuración crítico y debe fallar
        // rápido (fail-fast). La validación con ValidateOnStart corre al construir el host (no de
        // forma perezosa en la primera request autenticada), así que un despliegue mal configurado
        // ni siquiera arranca ni pasa health checks.
        var permiteClaveEfimera = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");

        servicios.AddOptions<OpcionesJwt>()
            .Bind(config.GetSection(OpcionesJwt.Seccion))
            .Validate(
                o => permiteClaveEfimera
                    || (!string.IsNullOrWhiteSpace(o.Key) && Encoding.UTF8.GetByteCount(o.Key) >= 32),
                "Jwt:Key es obligatorio y debe tener al menos 32 bytes fuera de Development/Testing")
            .ValidateOnStart();

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                // Sin este ajuste, JwtSecurityTokenHandler remapea automáticamente el claim "sub"
                // (JwtRegisteredClaimNames.Sub) a ClaimTypes.NameIdentifier al deserializar el
                // token entrante. Se desactiva ese mapeo para que los endpoints (p. ej. /api/perfil)
                // puedan leer el userId directamente vía User.FindFirst(JwtRegisteredClaimNames.Sub),
                // igual que se emitió en GeneradorTokensAcceso.
                opt.MapInboundClaims = false;

                var o = config.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();

                // La validación al arranque (ValidateOnStart, arriba) ya garantiza que fuera de
                // Development/Testing "Jwt:Key" está presente y tiene al menos 32 bytes; aquí solo
                // queda cubrir el caso dev/test sin clave configurada con una efímera de repuesto.
                string claveTexto;
                if (!string.IsNullOrWhiteSpace(o.Key))
                {
                    claveTexto = o.Key;
                }
                else
                {
                    // Clave efímera solo para que la construcción de las opciones no falle en
                    // dev/test; nunca podrá validar tokens reales (se firman con la clave real
                    // provista por user-secrets/env, no con esta clave de repuesto).
                    claveTexto = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }

                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveTexto)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        servicios.AddAuthorization();

        return servicios;
    }
}
