using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure.Resenas;

public sealed class RepositorioResenasEfCore(ReputationDbContext db)
    : IRepositorioResenas
{
    public Task<bool> ExisteAsync(
        Guid orderId,
        Guid autorId,
        CancellationToken ct) =>
        db.Reviews.AnyAsync(
            resena => resena.OrderId == orderId && resena.AutorId == autorId,
            ct);

    public void Agregar(Resena resena) => db.Reviews.Add(resena);
}
