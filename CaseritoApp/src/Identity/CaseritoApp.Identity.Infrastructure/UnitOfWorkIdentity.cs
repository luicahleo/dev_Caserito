using CaseritoApp.BuildingBlocks.Application.Abstractions;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> para el contexto de Identity: delega en
/// <see cref="IdentityDbContext.SaveChangesAsync(CancellationToken)"/>. La requiere
/// <c>UnitOfWorkBehavior</c> (pipeline de MediatR) para cualquier <c>ICommand</c>/<c>IQuery</c>
/// manejado dentro de este contexto (p. ej. <c>ActualizarPerfilCommand</c>).
/// </summary>
public sealed class UnitOfWorkIdentity(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> GuardarCambiosAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
