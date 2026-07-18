using CaseritoApp.Catalog.Application.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class BuscarAvisosQueryValidatorTests
{
    private readonly BuscarAvisosQueryValidator _validator = new();

    private static BuscarAvisosQuery Query(
        int pagina = 1, int tamano = 20, decimal? min = null, decimal? max = null, string? condicion = null) =>
        new(null, null, null, min, max, condicion, pagina, tamano);

    [Fact]
    public void Sin_parametros_es_valida()
    {
        var r = _validator.Validate(Query());
        Assert.True(r.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Pagina_menor_a_uno_falla(int pagina)
    {
        var r = _validator.Validate(Query(pagina: pagina));
        Assert.False(r.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Tamano_fuera_de_rango_falla(int tamano)
    {
        var r = _validator.Validate(Query(tamano: tamano));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Tamano_en_el_limite_superior_es_valido()
    {
        var r = _validator.Validate(Query(tamano: 50));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Precio_min_mayor_que_max_falla()
    {
        var r = _validator.Validate(Query(min: 100m, max: 10m));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Precio_negativo_falla()
    {
        var r = _validator.Validate(Query(min: -1m));
        Assert.False(r.IsValid);
    }

    [Theory]
    [InlineData("Nuevo")]
    [InlineData("usado")]
    [InlineData(null)]
    public void Condicion_valida_o_nula_pasa(string? condicion)
    {
        var r = _validator.Validate(Query(condicion: condicion));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Condicion_invalida_falla()
    {
        var r = _validator.Validate(Query(condicion: "Reacondicionado"));
        Assert.False(r.IsValid);
    }
}
