using System.Text;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        servicios.Configure<OpcionesJwt>(config.GetSection(OpcionesJwt.Seccion));
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton<IGeneradorTokensAcceso, GeneradorTokensAcceso>();
        servicios.AddScoped<IServicioRefreshTokens, ServicioRefreshTokens>();

        // Solo en Development/Testing se admite una clave efímera de repuesto; fuera de esos
        // entornos la ausencia de "Jwt:Key" es un error de configuración crítico y debe fallar
        // rápido (fail-fast), en lugar de sustituir silenciosamente por una clave que haría
        // rechazar todos los tokens con un 401 sin pista.
        var permiteClaveEfimera = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                var o = config.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();

                string claveTexto;
                if (!string.IsNullOrWhiteSpace(o.Key))
                {
                    claveTexto = o.Key;
                }
                else if (permiteClaveEfimera)
                {
                    // Clave efímera solo para que la construcción de las opciones no falle en
                    // dev/test; nunca podrá validar tokens reales (se firman con la clave real
                    // provista por user-secrets/env, no con esta clave de repuesto).
                    claveTexto = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }
                else
                {
                    throw new InvalidOperationException("Jwt:Key es obligatorio fuera de Development/Testing");
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
