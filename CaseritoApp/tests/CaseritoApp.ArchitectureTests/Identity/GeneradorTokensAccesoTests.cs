using System.IdentityModel.Tokens.Jwt;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CaseritoApp.ArchitectureTests.Identity;

public sealed class GeneradorTokensAccesoTests
{
    [Fact]
    public void Genera_jwt_con_claims_del_usuario()
    {
        var opciones = Options.Create(new OpcionesJwt
        {
            Key = new string('k', 40),
            Issuer = "CaseritoApp",
            Audience = "CaseritoApp",
            MinutosAcceso = 15,
        });
        var tiempo = new FakeTimeProvider();
        var proveedorClave = new ProveedorClaveFirma(opciones);
        var gen = new GeneradorTokensAcceso(opciones, proveedorClave, tiempo);
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.com", Nombres = "Ana" };

        var jwt = gen.Generar(usuario, [], verificado: false);
        var leido = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Equal(usuario.Id.ToString(), leido.Subject);
        Assert.Contains(leido.Claims, c => c.Type == "email" && c.Value == "a@b.com");
        Assert.Contains(leido.Claims, c => c.Type == "name" && c.Value == "Ana");
    }

    [Fact]
    public void Expira_a_los_minutos_configurados()
    {
        var opciones = Options.Create(new OpcionesJwt
        {
            Key = new string('k', 40),
            Issuer = "CaseritoApp",
            Audience = "CaseritoApp",
            MinutosAcceso = 15,
        });
        var tiempo = new FakeTimeProvider();
        var ahora = tiempo.GetUtcNow();
        var proveedorClave = new ProveedorClaveFirma(opciones);
        var gen = new GeneradorTokensAcceso(opciones, proveedorClave, tiempo);
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.com", Nombres = "Ana" };

        var jwt = gen.Generar(usuario, [], verificado: false);
        var leido = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Equal(ahora.AddMinutes(15).UtcDateTime, leido.ValidTo);
        Assert.Equal(ahora.UtcDateTime, leido.ValidFrom);
    }
}
