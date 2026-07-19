using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de los handlers de subida y borrado de fotos.</summary>
public sealed class SubirBorrarFotoCommandHandlerTests
{
    private static readonly byte[] _jpegValido = [0xFF, 0xD8, 0xFF, 0x00, 0x01];
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");

    // ── Fakes manuales ───────────────────────────────────────────────────

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso aviso) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private sealed class AlmacenFake(bool fallaGuardar = false) : IAlmacenFotosAviso
    {
        public List<string> ClavesGuardadas { get; } = [];
        public List<string> ClavesEliminadas { get; } = [];

        public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
        {
            if (fallaGuardar)
            {
                throw new IOException("disco lleno");
            }
            var clave = Guid.NewGuid().ToString("N");
            ClavesGuardadas.Add(clave);
            return Task.FromResult(clave);
        }

        public Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct) =>
            Task.FromResult((Array.Empty<byte>(), "image/jpeg"));

        public Task EliminarAsync(string clave, CancellationToken ct)
        {
            ClavesEliminadas.Add(clave);
            return Task.CompletedTask;
        }
    }

    private static Aviso AvisoActivo(Guid vendedorId) => Aviso.Crear(
        vendedorId, "Titulo", "Descripcion larga para el test.",
        Dinero.Crear(100m, Moneda.BOB).Valor,
        _categoria, _ciudad, CondicionArticulo.Nuevo, DateTime.UtcNow);

    // ── SubirFotoAvisoCommand ────────────────────────────────────────────

    [Fact]
    public async Task SubirFoto_AvisoNoEncontrado_RetornaNoEncontrado()
    {
        var almacen = new AlmacenFake();
        var handler = new SubirFotoAvisoCommandHandler(
            new RepositorioFake(null), almacen,
            NullLogger<SubirFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubirFotoAvisoCommand(Guid.NewGuid(), Guid.NewGuid(), _jpegValido, "image/jpeg"),
            default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, result.Error.Code);
        Assert.Empty(almacen.ClavesGuardadas);
    }

    [Fact]
    public async Task SubirFoto_NoEsPropietario_RetornaForbidden()
    {
        var aviso = AvisoActivo(Guid.NewGuid());
        var almacen = new AlmacenFake();
        var handler = new SubirFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<SubirFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubirFotoAvisoCommand(aviso.Id, Guid.NewGuid(), _jpegValido, "image/jpeg"),
            default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.NoEsPropietario, result.Error.Code);
        Assert.Empty(almacen.ClavesGuardadas);
    }

    [Fact]
    public async Task SubirFoto_ImagenInvalida_RetornaError_SinGuardarBlob()
    {
        var vendedorId = Guid.NewGuid();
        var aviso = AvisoActivo(vendedorId);
        var almacen = new AlmacenFake();
        var handler = new SubirFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<SubirFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubirFotoAvisoCommand(aviso.Id, vendedorId, [0x00, 0x01, 0x02], "image/jpeg"),
            default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.ImagenInvalida, result.Error.Code);
        Assert.Empty(almacen.ClavesGuardadas);
    }

    [Fact]
    public async Task SubirFoto_ErrorAlGuardarBlob_RetornaError()
    {
        var vendedorId = Guid.NewGuid();
        var aviso = AvisoActivo(vendedorId);
        var almacen = new AlmacenFake(fallaGuardar: true);
        var handler = new SubirFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<SubirFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubirFotoAvisoCommand(aviso.Id, vendedorId, _jpegValido, "image/jpeg"),
            default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.ErrorAlmacenamiento, result.Error.Code);
    }

    [Fact]
    public async Task SubirFoto_Exitoso_RetornaIdFoto()
    {
        var vendedorId = Guid.NewGuid();
        var aviso = AvisoActivo(vendedorId);
        var almacen = new AlmacenFake();
        var handler = new SubirFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<SubirFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubirFotoAvisoCommand(aviso.Id, vendedorId, _jpegValido, "image/jpeg"),
            default);

        Assert.True(result.EsExito);
        Assert.NotEqual(Guid.Empty, result.Valor);
        Assert.Single(aviso.Fotos);
        Assert.Single(almacen.ClavesGuardadas);
    }

    // ── BorrarFotoAvisoCommand ───────────────────────────────────────────

    [Fact]
    public async Task BorrarFoto_FotoInexistente_RetornaError()
    {
        var vendedorId = Guid.NewGuid();
        var aviso = AvisoActivo(vendedorId);
        var almacen = new AlmacenFake();
        var handler = new BorrarFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<BorrarFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new BorrarFotoAvisoCommand(aviso.Id, Guid.NewGuid(), vendedorId),
            default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.FotoNoEncontrada, result.Error.Code);
    }

    [Fact]
    public async Task BorrarFoto_Exitoso_BorraBlob()
    {
        var vendedorId = Guid.NewGuid();
        var aviso = AvisoActivo(vendedorId);
        aviso.AgregarFoto("mi-clave", "image/jpeg");
        var fotoId = aviso.Fotos[0].Id;
        var almacen = new AlmacenFake();
        var handler = new BorrarFotoAvisoCommandHandler(
            new RepositorioFake(aviso), almacen,
            NullLogger<BorrarFotoAvisoCommandHandler>.Instance);

        var result = await handler.Handle(
            new BorrarFotoAvisoCommand(aviso.Id, fotoId, vendedorId),
            default);

        Assert.True(result.EsExito);
        Assert.Empty(aviso.Fotos);
        Assert.Contains("mi-clave", almacen.ClavesEliminadas);
    }
}
