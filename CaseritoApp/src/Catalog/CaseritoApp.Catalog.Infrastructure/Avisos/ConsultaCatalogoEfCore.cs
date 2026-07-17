using CaseritoApp.Catalog.Application.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Adaptador EF Core de <see cref="IConsultaCatalogo"/>.</summary>
public sealed class ConsultaCatalogoEfCore(CatalogDbContext db) : IConsultaCatalogo
{
    public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct) =>
        db.Categorias.AnyAsync(c => c.Id == categoriaId && c.Activa, ct);

    public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct) =>
        db.Ciudades.AnyAsync(c => c.Id == ciudadId && c.Activa, ct);

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
        await db.Categorias
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .Select(c => new CategoriaDto(c.Id, c.Nombre))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
        await db.Ciudades
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .Select(c => new CiudadDto(c.Id, c.Nombre))
            .ToListAsync(ct);
}
