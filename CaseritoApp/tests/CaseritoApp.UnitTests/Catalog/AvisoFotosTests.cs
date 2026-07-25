using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de <see cref="Aviso.AgregarFoto"/> y <see cref="Aviso.QuitarFoto"/>.</summary>
public sealed class AvisoFotosTests
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");

    private static Aviso AvisoActivo() => Aviso.Crear(
        Guid.NewGuid(),
        "Título de prueba",
        "Descripción de prueba suficientemente larga.",
        Dinero.Crear(100m, Moneda.BOB).Valor,
        _categoria,
        _ciudad,
        CondicionArticulo.Nuevo,
        DateTime.UtcNow);

    [Fact]
    public void AgregarFoto_PrimeraFoto_TieneOrdenCero()
    {
        var aviso = AvisoActivo();

        var resultado = aviso.AgregarFoto("clave1", "image/jpeg");

        Assert.True(resultado.EsExito);
        Assert.Single(aviso.Fotos);
        Assert.Equal(0, aviso.Fotos[0].Orden);
        Assert.Equal("clave1", aviso.Fotos[0].Clave);
    }

    [Fact]
    public void AgregarFoto_SegundaFoto_TieneOrdenUno()
    {
        var aviso = AvisoActivo();
        aviso.AgregarFoto("clave1", "image/jpeg");

        aviso.AgregarFoto("clave2", "image/png");

        Assert.Equal(2, aviso.Fotos.Count);
        Assert.Equal(1, aviso.Fotos[1].Orden);
    }

    [Fact]
    public void AgregarFoto_SextaFoto_DevuelveErrorLimite()
    {
        var aviso = AvisoActivo();
        for (var i = 0; i < 5; i++)
        {
            aviso.AgregarFoto($"clave{i}", "image/jpeg");
        }

        var resultado = aviso.AgregarFoto("clave6", "image/jpeg");

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.LimiteFotosAlcanzado, resultado.Error.Code);
        Assert.Equal(5, aviso.Fotos.Count);
    }

    [Fact]
    public void QuitarFoto_FotoExistente_LaRemueveYDevuelveLaClave()
    {
        var aviso = AvisoActivo();
        aviso.AgregarFoto("clave1", "image/jpeg");
        var fotoId = aviso.Fotos[0].Id;

        var resultado = aviso.QuitarFoto(fotoId);

        Assert.True(resultado.EsExito);
        Assert.Equal("clave1", resultado.Valor.Clave);
        Assert.Empty(aviso.Fotos);
    }

    [Fact]
    public void QuitarFoto_FotoInexistente_DevuelveError()
    {
        var aviso = AvisoActivo();

        var resultado = aviso.QuitarFoto(Guid.NewGuid());

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.FotoNoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public void Vendido_rechaza_agregar_y_quitar_fotos()
    {
        var aviso = AvisoActivo();
        Assert.True(aviso.AgregarFoto("clave1", "image/jpeg").EsExito);
        var fotoId = aviso.Fotos[0].Id;
        Assert.True(aviso.MarcarVendido(
            Guid.NewGuid(), aviso.VendedorId, DateTime.UtcNow).EsExito);

        var agregar = aviso.AgregarFoto("clave2", "image/jpeg");
        var quitar = aviso.QuitarFoto(fotoId);

        Assert.False(agregar.EsExito);
        Assert.False(quitar.EsExito);
        Assert.Equal(ErroresAviso.TransicionInvalida, agregar.Error.Code);
        Assert.Equal(ErroresAviso.TransicionInvalida, quitar.Error.Code);
    }
}
