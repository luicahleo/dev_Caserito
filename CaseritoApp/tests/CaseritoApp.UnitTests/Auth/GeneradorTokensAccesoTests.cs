using System.IdentityModel.Tokens.Jwt;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class GeneradorTokensAccesoTests
{
    private static GeneradorTokensAcceso CrearGenerador()
    {
        var opciones = Options.Create(new OpcionesJwt { Key = "clave-fija-para-tests-unitarios-de-32b+" });
        return new GeneradorTokensAcceso(opciones, new ProveedorClaveFirma(opciones), TimeProvider.System);
    }

    private static JwtSecurityToken Leer(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

    [Fact]
    public void Emite_un_claim_perm_por_permiso()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(usuario, [Permisos.KycRevisar, Permisos.UsuariosGestionar], verificado: false);

        var permisos = Leer(jwt).Claims.Where(c => c.Type == ClaimsApp.Permiso).Select(c => c.Value).ToArray();
        Assert.Contains(Permisos.KycRevisar, permisos);
        Assert.Contains(Permisos.UsuariosGestionar, permisos);
        Assert.Equal(2, permisos.Length);
    }

    [Fact]
    public void Sin_permisos_no_emite_claims_perm()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(usuario, [], verificado: false);

        Assert.DoesNotContain(Leer(jwt).Claims, c => c.Type == ClaimsApp.Permiso);
    }

    [Fact]
    public void Generar_incluye_claim_verificado_true_cuando_esta_verificado()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(usuario, [], verificado: true);

        Assert.Equal("true", Leer(jwt).Claims.Single(c => c.Type == "verificado").Value);
    }

    [Fact]
    public void Generar_distingue_identidad_habilitada_de_kyc_verificado()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(
            usuario, [], verificado: false, identidadHabilitada: true);

        var claims = Leer(jwt).Claims;
        Assert.Equal("false", claims.Single(c => c.Type == ClaimsApp.Verificado).Value);
        Assert.Equal("true", claims.Single(c => c.Type == ClaimsApp.IdentidadHabilitada).Value);
    }
}
