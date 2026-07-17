using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> para el contexto Catalog: delega en
/// <see cref="CatalogDbContext.SaveChangesAsync(CancellationToken)"/> y traduce el conflicto de
/// concurrencia a la excepción neutral <see cref="ConflictoConcurrenciaException"/>.
/// </summary>
public sealed class UnitOfWorkCatalog(CatalogDbContext dbContext) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir los cambios.", ex);
        }
    }
}
