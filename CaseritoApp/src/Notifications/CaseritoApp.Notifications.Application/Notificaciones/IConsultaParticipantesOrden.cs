namespace CaseritoApp.Notifications.Application.Notificaciones;

/// <summary>
/// Puerto de aplicación para obtener los participantes de una orden sin revelar
/// detalles del bounded context Orders.
/// </summary>
public interface IConsultaParticipantesOrden
{
    public Task<ParticipantesOrdenDto?> ObtenerAsync(Guid ordenId, CancellationToken ct);
}

public sealed record ParticipantesOrdenDto(Guid CompradorId, Guid VendedorId);
