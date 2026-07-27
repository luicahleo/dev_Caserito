using CaseritoApp.Notifications.Domain.PuntosEncuentro;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class PuntoEncuentroSeguroTests
{
    [Fact]
    public void Crear_ConDatosValidos_CreaPuntoActivoNormalizado()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio Farmacia Boliviana",
            "Cochabamba",
            "Calle España esq. Ayacucho");

        Assert.True(resultado.EsExito);
        Assert.Equal("Edificio Farmacia Boliviana", resultado.Valor.Nombre);
        Assert.Equal("cochabamba", resultado.Valor.Ciudad);
        Assert.Equal("Calle España esq. Ayacucho", resultado.Valor.Direccion);
        Assert.True(resultado.Valor.Activo);
    }

    [Fact]
    public void Crear_ConIdExplicito_UsaIdProporcionado()
    {
        var id = Guid.NewGuid();

        var resultado = PuntoEncuentroSeguro.Crear(
            "Catedral San Sebastián",
            "Cochabamba",
            "Plaza 14 de Septiembre",
            id: id);

        Assert.True(resultado.EsExito);
        Assert.Equal(id, resultado.Valor.Id);
    }

    [Fact]
    public void Crear_NombreVacio_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "   ",
            "Cochabamba",
            "Calle España");

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_CiudadVacia_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio Farmacia Boliviana",
            string.Empty,
            "Calle España");

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_DireccionVacia_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio Farmacia Boliviana",
            "Cochabamba",
            null!);

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_NombreMuyLargo_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            new string('x', 151),
            "Cochabamba",
            "Calle España");

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_CiudadMuyLarga_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio",
            new string('x', 101),
            "Calle España");

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_DireccionMuyLarga_Rechaza()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio",
            "Cochabamba",
            new string('x', 251));

        Assert.False(resultado.EsExito);
        Assert.Equal("punto_encuentro_invalido", resultado.Error.Code);
    }

    [Fact]
    public void Crear_ConActivoFalse_CreaInactivo()
    {
        var resultado = PuntoEncuentroSeguro.Crear(
            "Edificio",
            "Cochabamba",
            "Calle España",
            activo: false);

        Assert.True(resultado.EsExito);
        Assert.False(resultado.Valor.Activo);
    }
}
