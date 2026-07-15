using CaseritoApp.Identity.Application.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class BuscarUsuariosQueryValidatorTests
{
    private readonly BuscarUsuariosQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 20)]   // pagina < 1
    [InlineData(1, 0)]    // tamano < 1
    [InlineData(1, 101)]  // tamano > 100
    public void Rechaza_paginacion_invalida(int pagina, int tamano)
    {
        var resultado = _validator.Validate(new BuscarUsuariosQuery(null, pagina, tamano));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Acepta_paginacion_valida()
    {
        var resultado = _validator.Validate(new BuscarUsuariosQuery("ana", 1, 20));

        Assert.True(resultado.IsValid);
    }
}
