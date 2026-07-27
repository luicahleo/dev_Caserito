using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> para el contexto Notifications: delega en
/// <see cref="NotificationsDbContext.SaveChangesAsync(CancellationToken)"/> y traduce el conflicto de
/// concurrencia a la excepción neutral <see cref="ConflictoConcurrenciaException"/>.
/// </summary>
public sealed class UnitOfWorkNotifications(NotificationsDbContext db) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir los cambios.", ex);
        }
    }
}
