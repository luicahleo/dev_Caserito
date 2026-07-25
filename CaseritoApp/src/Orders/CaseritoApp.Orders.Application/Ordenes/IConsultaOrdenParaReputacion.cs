namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record ReferenciaOrdenParaReputacion(
    Guid OrderId,
    Guid CompradorId,
    Guid VendedorId,
    string Estado);

public interface IConsultaOrdenParaReputacion
{
    public Task<ReferenciaOrdenParaReputacion?> ObtenerAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken ct);
}
