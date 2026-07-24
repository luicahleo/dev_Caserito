namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record AvisoParaOrden(
    Guid AvisoId,
    Guid VendedorId,
    string Titulo,
    decimal Monto,
    string Moneda);

public interface IConsultaAvisoParaOrden
{
    public Task<AvisoParaOrden?> ObtenerAsync(Guid avisoId, CancellationToken ct);
}
