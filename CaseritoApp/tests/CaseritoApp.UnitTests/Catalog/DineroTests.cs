using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class DineroTests
{
    [Fact]
    public void Crear_con_monto_positivo_es_exito()
    {
        var resultado = Dinero.Crear(150.50m, Moneda.BOB);

        Assert.True(resultado.EsExito);
        Assert.Equal(150.50m, resultado.Valor.Monto);
        Assert.Equal(Moneda.BOB, resultado.Valor.Moneda);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_con_monto_no_positivo_falla_con_precio_invalido(decimal monto)
    {
        var resultado = Dinero.Crear(monto, Moneda.BOB);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.PrecioInvalido, resultado.Error.Code);
    }

    [Fact]
    public void Dos_dineros_iguales_por_valor_son_iguales()
    {
        var a = Dinero.Crear(10m, Moneda.BOB).Valor;
        var b = Dinero.Crear(10m, Moneda.BOB).Valor;

        Assert.Equal(a, b);
    }
}
