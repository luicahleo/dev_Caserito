using CaseritoApp.Catalog.Infrastructure.Fotos;
using SkiaSharp;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ProcesadorFotoAvisoSkiaTests
{
    [Fact]
    public async Task Procesar_JpegGrande_LimitaDimensionesPesoYEliminaMetadata()
    {
        var entrada = CrearImagen(2400, 1200, SKColors.Red, SKEncodedImageFormat.Jpeg);
        var procesador = new ProcesadorFotoAvisoSkia();

        var resultado = await procesador.ProcesarAsync(entrada, "image/jpeg", default);

        Assert.NotNull(resultado);
        Assert.Equal("image/jpeg", resultado.ContentType);
        Assert.True(resultado.Contenido.Length <= 1024 * 1024);
        using var imagen = SKBitmap.Decode(resultado.Contenido);
        Assert.Equal(1600, imagen.Width);
        Assert.Equal(800, imagen.Height);
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(resultado.Contenido));
    }

    [Fact]
    public async Task Procesar_PngTransparente_ComponeSobreBlanco()
    {
        var entrada = CrearImagen(20, 20, SKColors.Transparent, SKEncodedImageFormat.Png);
        var procesador = new ProcesadorFotoAvisoSkia();

        var resultado = await procesador.ProcesarAsync(entrada, "image/png", default);

        Assert.NotNull(resultado);
        using var imagen = SKBitmap.Decode(resultado.Contenido);
        var pixel = imagen.GetPixel(10, 10);
        Assert.True(pixel.Red > 245 && pixel.Green > 245 && pixel.Blue > 245);
    }

    [Fact]
    public async Task Procesar_JpegConOrientacionDerecha_RotaFisicamenteLaSalida()
    {
        var jpeg = CrearImagen(20, 10, SKColors.Blue, SKEncodedImageFormat.Jpeg);
        var entrada = InsertarOrientacionExif(jpeg, 6);
        var procesador = new ProcesadorFotoAvisoSkia();

        var resultado = await procesador.ProcesarAsync(entrada, "image/jpeg", default);

        Assert.NotNull(resultado);
        using var imagen = SKBitmap.Decode(resultado.Contenido);
        Assert.Equal(10, imagen.Width);
        Assert.Equal(20, imagen.Height);
    }

    [Fact]
    public async Task Procesar_ContenidoCorrupto_RetornaNull()
    {
        var procesador = new ProcesadorFotoAvisoSkia();

        var resultado = await procesador.ProcesarAsync([0xFF, 0xD8, 0xFF, 0x00], "image/jpeg", default);

        Assert.Null(resultado);
    }

    private static byte[] CrearImagen(int ancho, int alto, SKColor color, SKEncodedImageFormat formato)
    {
        using var bitmap = new SKBitmap(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(formato, 95);
        return datos.ToArray();
    }

    private static byte[] InsertarOrientacionExif(byte[] jpeg, ushort orientacion)
    {
        byte[] segmento =
        [
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            (byte)orientacion, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. segmento, .. jpeg[2..]];
    }
}
