using System.Text;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public static IServiceCollection AgregarAutenticacionJwt(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<OpcionesJwt>(config.GetSection(OpcionesJwt.Seccion));
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton<IGeneradorTokensAcceso, GeneradorTokensAcceso>();

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                var o = config.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();

                // Sin "Jwt:Key" configurado (p. ej. hosts/tests que no usan autenticación), se
                // recurre a una clave efímera solo para que la construcción de las opciones no
                // falle: nunca podrá validar tokens reales, porque estos se firman con la clave
                // real provista por user-secrets/env, no con esta clave de repuesto.
                var claveTexto = string.IsNullOrWhiteSpace(o.Key)
                    ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
                    : o.Key;

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
