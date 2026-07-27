using CaseritoApp.Notifications.Domain.Busquedas;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class BusquedaGuardadaTests
{
    [Fact]
    public void Crear_ConPalabraClave_CreaBusquedaNormalizada()
    {
        var usuarioId = Guid.NewGuid();

        var resultado = BusquedaGuardada.Crear(
            usuarioId,
            "  iPhone  ",
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.True(resultado.EsExito);
        Assert.Equal(usuarioId, resultado.Valor.UsuarioId);
        Assert.Equal("iphone", resultado.Valor.PalabraClave);
        Assert.Null(resultado.Valor.Categoria);
    }

    [Fact]
    public void Crear_ConCategoriaYCiudad_CreaBusquedaNormalizada()
    {
        var usuarioId = Guid.NewGuid();

        var resultado = BusquedaGuardada.Crear(
            usuarioId,
            null,
            "Tecnología",
            "Cochabamba",
            100,
            500,
            "Usado",
            DateTimeOffset.UtcNow);

        Assert.True(resultado.EsExito);
        Assert.Equal("tecnología", resultado.Valor.Categoria);
        Assert.Equal("cochabamba", resultado.Valor.Ciudad);
        Assert.Equal("usado", resultado.Valor.EstadoProducto);
        Assert.Equal(100, resultado.Valor.PrecioMinimo);
        Assert.Equal(500, resultado.Valor.PrecioMaximo);
    }

    [Fact]
    public void Crear_SinCriterios_Rechaza()
    {
        var resultado = BusquedaGuardada.Crear(
            Guid.NewGuid(),
            null,
            "   ",
            string.Empty,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal("busqueda_guardada_invalida", resultado.Error.Code);
    }

    [Fact]
    public void Crear_UsuarioVacio_Rechaza()
    {
        var resultado = BusquedaGuardada.Crear(
            Guid.Empty,
            "iPhone",
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
    }

    [Fact]
    public void Crear_PrecioMinimoMayorQueMaximo_Rechaza()
    {
        var resultado = BusquedaGuardada.Crear(
            Guid.NewGuid(),
            "iPhone",
            null,
            null,
            500,
            100,
            null,
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal("busqueda_guardada_invalida", resultado.Error.Code);
    }

    public static TheoryData<decimal?, decimal?> PreciosNegativos => new()
    {
        { -1.0m, null },
        { null, -1.0m },
        { -10.0m, -20.0m },
    };

    [Theory]
    [MemberData(nameof(PreciosNegativos))]
    public void Crear_PrecioNegativo_Rechaza(decimal? precioMinimo, decimal? precioMaximo)
    {
        var resultado = BusquedaGuardada.Crear(
            Guid.NewGuid(),
            "iPhone",
            null,
            null,
            precioMinimo,
            precioMaximo,
            null,
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal("busqueda_guardada_invalida", resultado.Error.Code);
    }

    [Fact]
    public void Crear_FechaNoUtc_AlmacenaUtc()
    {
        var creadaEn = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(-4));

        var resultado = BusquedaGuardada.Crear(
            Guid.NewGuid(),
            "iPhone",
            null,
            null,
            null,
            null,
            null,
            creadaEn);

        Assert.True(resultado.EsExito);
        Assert.Equal(creadaEn.ToUniversalTime(), resultado.Valor.CreadaEn);
    }
}
