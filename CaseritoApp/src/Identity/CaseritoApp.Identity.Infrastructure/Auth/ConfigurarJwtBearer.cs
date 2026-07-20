using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>
/// Configura los <see cref="JwtBearerOptions"/> del esquema Bearer usando el <see cref="OpcionesJwt"/>
/// ya bindeado/validado y la clave compartida de <see cref="ProveedorClaveFirma"/> (misma clave que
/// usa el generador de tokens, de modo que firma y validación nunca divergen).
/// </summary>
public sealed class ConfigurarJwtBearer(IOptions<OpcionesJwt> opciones, ProveedorClaveFirma proveedorClave)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly OpcionesJwt _o = opciones.Value;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        // Ver nota en DependencyInjection: se desactiva el remapeo de "sub" para leer el userId
        // directamente en los endpoints.
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = contexto =>
            {
                var token = contexto.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token)
                    && contexto.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    contexto.Token = token;
                }

                return Task.CompletedTask;
            },
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _o.Issuer,
            ValidateAudience = true,
            ValidAudience = _o.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = proveedorClave.Clave,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
