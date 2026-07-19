using CaseritoApp.Catalog.Application.Fotos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de <see cref="ValidacionFotoAviso"/>.</summary>
public sealed class ValidacionFotoAvisoTests
{
    private static readonly byte[] _jpegValido = [0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02];
    private static readonly byte[] _pngValido = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] _invalido = [0x00, 0x01, 0x02, 0x03];

    [Fact]
    public void EsImagenValida_JpegConFirmaCorrecta_RetornaTrue()
    {
        Assert.True(ValidacionFotoAviso.EsImagenValida(_jpegValido, "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_PngConFirmaCorrecta_RetornaTrue()
    {
        Assert.True(ValidacionFotoAviso.EsImagenValida(_pngValido, "image/png"));
    }

    [Fact]
    public void EsImagenValida_MagicBytesIncorrectos_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida(_invalido, "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_ContentTypeNoPermitido_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida(_jpegValido, "image/gif"));
    }

    [Fact]
    public void EsImagenValida_ContenidoVacio_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida([], "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_TamanoExcedido_RetornaFalse()
    {
        var grande = new byte[ValidacionFotoAviso.LimiteBytes + 1];
        grande[0] = 0xFF; grande[1] = 0xD8; grande[2] = 0xFF;
        Assert.False(ValidacionFotoAviso.EsImagenValida(grande, "image/jpeg"));
    }
}
