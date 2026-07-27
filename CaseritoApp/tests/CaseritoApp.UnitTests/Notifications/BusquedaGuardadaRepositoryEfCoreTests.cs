using CaseritoApp.Notifications.Domain.Busquedas;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Notifications.Infrastructure.Busquedas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class BusquedaGuardadaRepositoryEfCoreTests : IDisposable
{
    private readonly NotificationsDbContext _db;
    private readonly BusquedaGuardadaRepositoryEfCore _repo;

    public BusquedaGuardadaRepositoryEfCoreTests()
    {
        var opciones = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase($"NotificationsTest-{Guid.NewGuid():N}")
            .Options;
        _db = new NotificationsDbContext(opciones);
        _repo = new BusquedaGuardadaRepositoryEfCore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ListarCoincidencias_CoincidePorCategoriaYCiudad_RetornaBusqueda()
    {
        var usuarioId = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioId, null, "tecnología", "cochabamba"));
        await _db.SaveChangesAsync();

        var coincidencias = await _repo.ListarCoincidenciasAsync(
            "iPhone 14",
            "tecnología",
            "cochabamba",
            3500,
            "usado",
            CancellationToken.None);

        Assert.Single(coincidencias);
        Assert.Equal(usuarioId, coincidencias[0].UsuarioId);
    }

    [Fact]
    public async Task ListarCoincidencias_PrecioFueraDeRango_NoRetornaBusqueda()
    {
        var usuarioId = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioId, "producto", null, null, 5000, 10000));
        await _db.SaveChangesAsync();

        var coincidencias = await _repo.ListarCoincidenciasAsync(
            "iPhone 14",
            null,
            null,
            3500,
            null,
            CancellationToken.None);

        Assert.Empty(coincidencias);
    }

    [Fact]
    public async Task ListarCoincidencias_PalabraClaveEnTitulo_RetornaBusqueda()
    {
        var usuarioId = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioId, "iphone", null, null));
        await _db.SaveChangesAsync();

        var coincidencias = await _repo.ListarCoincidenciasAsync(
            "Vendo iPhone 14",
            null,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Single(coincidencias);
    }

    [Fact]
    public async Task ListarCoincidencias_PalabraClaveNoEnTitulo_NoRetornaBusqueda()
    {
        var usuarioId = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioId, "samsung", null, null));
        await _db.SaveChangesAsync();

        var coincidencias = await _repo.ListarCoincidenciasAsync(
            "Vendo iPhone 14",
            null,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Empty(coincidencias);
    }

    [Fact]
    public async Task ListarCoincidencias_EstadoCualquiera_RetornaParaCualquierEstado()
    {
        var usuarioId = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioId, null, "tecnología", null, null, null, "cualquiera"));
        await _db.SaveChangesAsync();

        var coincidencias = await _repo.ListarCoincidenciasAsync(
            null,
            "tecnología",
            null,
            null,
            "usado",
            CancellationToken.None);

        Assert.Single(coincidencias);
    }

    [Fact]
    public async Task ContarPorUsuarioAsync_CuentaSoloDelUsuario()
    {
        var usuarioA = Guid.NewGuid();
        var usuarioB = Guid.NewGuid();
        _repo.Agregar(CrearBusqueda(usuarioA, "a", null, null));
        _repo.Agregar(CrearBusqueda(usuarioA, "b", null, null));
        _repo.Agregar(CrearBusqueda(usuarioB, "c", null, null));
        await _db.SaveChangesAsync();

        var cantidad = await _repo.ContarPorUsuarioAsync(usuarioA, CancellationToken.None);

        Assert.Equal(2, cantidad);
    }

    [Fact]
    public async Task Eliminar_EliminaBusquedaDelUsuario()
    {
        var usuarioId = Guid.NewGuid();
        var busqueda = CrearBusqueda(usuarioId, "iphone", null, null);
        _repo.Agregar(busqueda);
        await _db.SaveChangesAsync();

        _repo.Eliminar(busqueda);
        await _db.SaveChangesAsync();

        var persistida = await _repo.ObtenerAsync(busqueda.Id, usuarioId, CancellationToken.None);
        Assert.Null(persistida);
    }

    private static BusquedaGuardada CrearBusqueda(
        Guid usuarioId,
        string? palabraClave,
        string? categoria,
        string? ciudad,
        decimal? precioMinimo = null,
        decimal? precioMaximo = null,
        string? estadoProducto = null)
    {
        return BusquedaGuardada.Crear(
            usuarioId,
            palabraClave,
            categoria,
            ciudad,
            precioMinimo,
            precioMaximo,
            estadoProducto,
            DateTimeOffset.UtcNow).Valor;
    }
}
