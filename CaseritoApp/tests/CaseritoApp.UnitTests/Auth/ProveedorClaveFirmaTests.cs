using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class ProveedorClaveFirmaTests
{
    [Fact]
    public void Clave_efimera_tiene_al_menos_256_bits_cuando_no_hay_key()
    {
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = string.Empty }));

        Assert.True(proveedor.Clave.KeySize >= 256);
    }

    [Fact]
    public void Clave_es_la_misma_instancia_en_lecturas_sucesivas()
    {
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = string.Empty }));

        Assert.Same(proveedor.Clave, proveedor.Clave);
    }

    [Fact]
    public void Usa_la_key_configurada_cuando_esta_presente()
    {
        const string key = "clave-fija-para-tests-unitarios-de-32b+";
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = key }));

        Assert.Equal(Encoding.UTF8.GetBytes(key), proveedor.Clave.Key);
    }

    [Fact]
    public void Token_firmado_con_clave_efimera_valida_con_la_misma_clave()
    {
        // Regresión del bug Jwt:Key: sin Key configurada, firma y validación deben usar la MISMA clave.
        var opciones = Options.Create(new OpcionesJwt { Key = string.Empty });
        var proveedor = new ProveedorClaveFirma(opciones);
        var generador = new GeneradorTokensAcceso(opciones, proveedor, TimeProvider.System);

        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };
        var jwt = generador.Generar(usuario);

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CaseritoApp",
            ValidateAudience = true,
            ValidAudience = "CaseritoApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = proveedor.Clave,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, parametros, out _);

        Assert.NotNull(principal);
    }
}
