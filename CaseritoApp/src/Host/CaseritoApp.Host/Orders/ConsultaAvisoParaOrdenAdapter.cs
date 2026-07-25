using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Orders.Application.Ordenes;

namespace CaseritoApp.Host.Orders;

public sealed class ConsultaAvisoParaOrdenAdapter(IConsultaAvisosPublica consulta)
    : IConsultaAvisoParaOrden
{
    public async Task<AvisoParaOrden?> ObtenerAsync(Guid avisoId, CancellationToken ct)
    {
        var referencia = await consulta.ObtenerReferenciaContactableAsync(avisoId, ct);
        return referencia is null
            ? null
            : new AvisoParaOrden(
                referencia.AvisoId,
                referencia.VendedorId,
                referencia.Titulo,
                referencia.Monto,
                referencia.Moneda);
    }
}
