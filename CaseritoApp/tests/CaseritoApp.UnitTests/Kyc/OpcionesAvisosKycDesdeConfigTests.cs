using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class OpcionesAvisosKycDesdeConfigTests
{
    [Fact]
    public void Expone_el_buzon_configurado()
    {
        var opciones = new OpcionesAvisosKycDesdeConfig(
            Options.Create(new OpcionesAvisosKyc { EmailAvisos = "admin@caserito.test" }));

        Assert.Equal("admin@caserito.test", opciones.EmailAvisos);
    }

    [Fact]
    public void Sin_configurar_no_expone_buzon()
    {
        var opciones = new OpcionesAvisosKycDesdeConfig(
            Options.Create(new OpcionesAvisosKyc()));

        Assert.True(string.IsNullOrWhiteSpace(opciones.EmailAvisos));
    }
}
