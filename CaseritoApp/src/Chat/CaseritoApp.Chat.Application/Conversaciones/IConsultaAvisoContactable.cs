namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record ReferenciaAvisoContactable(Guid AvisoId, Guid VendedorId);

public interface IConsultaAvisoContactable
{
    public Task<ReferenciaAvisoContactable?> ObtenerAsync(Guid avisoId, CancellationToken ct);
}
