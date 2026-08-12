using CaseritoApp.Catalog.Infrastructure.Fotos;
using Microsoft.Extensions.Options;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class AlmacenFotoAvisoDiscoTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"fotos-aviso-{Guid.NewGuid():N}");

    [Fact]
    public async Task Guardar_Jpeg_UsaClaveOpacaParticionadaSinMetadataSeparada()
    {
        var almacen = CrearAlmacen();
        byte[] contenido = [0xFF, 0xD8, 0xFF, 0x01];

        var clave = await almacen.GuardarAsync(contenido, "image/jpeg", default);

        Assert.Equal(32, clave.Length);
        Assert.True(Guid.TryParseExact(clave, "N", out _));
        var ruta = Path.Combine(_ruta, clave[..2], clave[2..4], $"{clave}.jpg");
        Assert.Equal(contenido, await File.ReadAllBytesAsync(ruta));
        Assert.Empty(Directory.GetFiles(_ruta, "*.meta", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(_ruta, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ObtenerYEliminar_BlobLegado_MantieneCompatibilidad()
    {
        Directory.CreateDirectory(_ruta);
        const string clave = "0123456789abcdef0123456789abcdef";
        byte[] contenido = [0x89, 0x50, 0x4E, 0x47];
        await File.WriteAllBytesAsync(Path.Combine(_ruta, $"{clave}.bin"), contenido);
        await File.WriteAllTextAsync(Path.Combine(_ruta, $"{clave}.meta"), "image/png");
        var almacen = CrearAlmacen();

        var resultado = await almacen.ObtenerAsync(clave, default);
        await almacen.EliminarAsync(clave, default);

        Assert.Equal(contenido, resultado.Contenido);
        Assert.Equal("image/png", resultado.ContentType);
        Assert.Empty(Directory.GetFiles(_ruta));
    }

    public void Dispose()
    {
        if (Directory.Exists(_ruta))
        {
            Directory.Delete(_ruta, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private AlmacenFotoAvisoDisco CrearAlmacen() =>
        new(Options.Create(new OpcionesAlmacenFotos { RutaBase = _ruta }));
}
