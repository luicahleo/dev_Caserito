using CaseritoApp.BuildingBlocks.Application.Abstractions;

namespace CaseritoApp.Notifications.Infrastructure;

public sealed class UnitOfWorkNotifications(NotificationsDbContext db) : IUnitOfWork
{
    public Task<int> GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
