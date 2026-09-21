using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class OpcionesResolucionKycDesdeConfigTests
{
    [Fact]
    public void Expone_el_umbral_configurado()
    {
        var opciones = new OpcionesResolucionKycDesdeConfig(
            Options.Create(new OpcionesArgos { UmbralAutoAprobacion = 72.5 }));

        Assert.IsAssignableFrom<IOpcionesResolucionKyc>(opciones);
        Assert.Equal(72.5, opciones.UmbralAutoAprobacionSimilitud);
    }

    [Fact]
    public void Sin_configuracion_explicita_el_umbral_por_defecto_es_60()
    {
        var opciones = new OpcionesResolucionKycDesdeConfig(
            Options.Create(new OpcionesArgos()));

        Assert.Equal(60, opciones.UmbralAutoAprobacionSimilitud);
    }
}
