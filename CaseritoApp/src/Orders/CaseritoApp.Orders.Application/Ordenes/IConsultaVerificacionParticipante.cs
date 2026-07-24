namespace CaseritoApp.Orders.Application.Ordenes;

public interface IConsultaVerificacionParticipante
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct);
}
