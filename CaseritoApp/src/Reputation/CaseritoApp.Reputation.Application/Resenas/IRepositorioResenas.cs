using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.Reputation.Application.Resenas;

public interface IRepositorioResenas
{
    public Task<bool> ExisteAsync(Guid orderId, Guid autorId, CancellationToken ct);

    public void Agregar(Resena resena);
}
