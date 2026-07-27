using CaseritoApp.Notifications.Domain.PuntosEncuentro;

namespace CaseritoApp.Notifications.Application.PuntosEncuentro;

public interface IPuntoEncuentroSeguroRepository
{
    public Task<IReadOnlyList<PuntoEncuentroSeguro>> ListarActivosPorCiudadAsync(
        string ciudad,
        CancellationToken ct);

    public Task<bool> ExisteAsync(Guid id, CancellationToken ct);

    public void Agregar(PuntoEncuentroSeguro punto);
}
