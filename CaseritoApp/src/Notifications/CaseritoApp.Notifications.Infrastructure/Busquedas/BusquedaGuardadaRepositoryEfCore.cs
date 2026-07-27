using System.Data;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Application.Busquedas;
using CaseritoApp.Notifications.Domain.Busquedas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Busquedas;

public sealed class BusquedaGuardadaRepositoryEfCore(NotificationsDbContext db)
    : IBusquedaGuardadaRepository
{
    public async Task<Result> AgregarConLimiteAsync(
        BusquedaGuardada busqueda,
        int limite,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var cantidad = await db.BusquedasGuardadas
            .CountAsync(b => b.UsuarioId == busqueda.UsuarioId, ct);

        if (cantidad >= limite)
        {
            await tx.RollbackAsync(ct);
            return Result.Fallo(new Error(
                "limite_busquedas_alcanzado",
                $"Se alcanzó el límite de {limite} búsquedas guardadas."));
        }

        db.BusquedasGuardadas.Add(busqueda);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Exito();
    }

    public Task<BusquedaGuardada?> ObtenerAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct)
    {
        return db.BusquedasGuardadas
            .FirstOrDefaultAsync(b => b.Id == id && b.UsuarioId == usuarioId, ct);
    }

    public async Task<IReadOnlyList<BusquedaGuardada>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct)
    {
        var busquedas = await db.BusquedasGuardadas
            .Where(b => b.UsuarioId == usuarioId)
            .OrderByDescending(b => b.CreadaEn)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);

        return busquedas;
    }

    public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        return db.BusquedasGuardadas.CountAsync(b => b.UsuarioId == usuarioId, ct);
    }

    public void Eliminar(BusquedaGuardada busqueda) => db.BusquedasGuardadas.Remove(busqueda);

    public async Task<IReadOnlyList<BusquedaGuardada>> ListarCoincidenciasAsync(
        string? tituloProducto,
        string? categoriaProducto,
        string? ciudadProducto,
        decimal? precioProducto,
        string? estadoProducto,
        CancellationToken ct)
    {
        var titulo = tituloProducto?.Trim().ToLowerInvariant();
        var categoria = categoriaProducto?.Trim().ToLowerInvariant();
        var ciudad = ciudadProducto?.Trim().ToLowerInvariant();
        var estado = estadoProducto?.Trim().ToLowerInvariant();

        var busquedas = await db.BusquedasGuardadas
            .AsNoTracking()
            .Where(b =>
                (string.IsNullOrEmpty(b.PalabraClave)
                 || (!string.IsNullOrEmpty(titulo) && titulo.Contains(b.PalabraClave)))
                && (string.IsNullOrEmpty(b.Categoria) || b.Categoria == categoria)
                && (string.IsNullOrEmpty(b.Ciudad) || b.Ciudad == ciudad)
                && (!b.PrecioMinimo.HasValue
                    || (precioProducto.HasValue && b.PrecioMinimo <= precioProducto))
                && (!b.PrecioMaximo.HasValue
                    || (precioProducto.HasValue && precioProducto <= b.PrecioMaximo))
                && (string.IsNullOrEmpty(b.EstadoProducto)
                    || b.EstadoProducto == BusquedaGuardada.EstadoCualquiera
                    || b.EstadoProducto == estado))
            .ToListAsync(ct);

        return busquedas;
    }
}
