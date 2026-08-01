using CaseritoApp.Identity.Infrastructure.Auth;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class ProveedorTokenRestablecimientoPasswordTests
{
    [Fact]
    public void Opciones_configuran_vigencia_de_treinta_minutos()
    {
        var opciones = new OpcionesTokenRestablecimientoPassword();

        Assert.Equal(TimeSpan.FromMinutes(30), opciones.TokenLifespan);
    }
}
