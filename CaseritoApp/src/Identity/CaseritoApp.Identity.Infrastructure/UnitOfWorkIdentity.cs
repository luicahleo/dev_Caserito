using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> para el contexto de Identity: delega en
/// <see cref="IdentityDbContext.SaveChangesAsync(CancellationToken)"/>. La requiere
/// <c>UnitOfWorkBehavior</c> (pipeline de MediatR) para cualquier <c>ICommand</c>/<c>IQuery</c>
/// manejado dentro de este contexto (p. ej. <c>ActualizarPerfilCommand</c>). Traduce
/// <see cref="DbUpdateConcurrencyException"/> a <see cref="ConflictoConcurrenciaException"/>
/// (neutral, sin dependencia de EF) para que la presentación la mapee a HTTP 409.
/// </summary>
public sealed class UnitOfWorkIdentity(IdentityDbContext dbContext) : IUnitOfWork
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
