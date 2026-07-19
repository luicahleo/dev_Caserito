using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Chat.Application.Conversaciones;

namespace CaseritoApp.Host.Chat;

public sealed class ConsultaAvisoContactableAdapter(IConsultaAvisosPublica consulta)
    : IConsultaAvisoContactable
{
    public async Task<ReferenciaAvisoContactable?> ObtenerAsync(
        Guid avisoId,
        CancellationToken ct)
    {
        var referencia = await consulta.ObtenerReferenciaContactableAsync(avisoId, ct);
        return referencia is null
            ? null
            : new ReferenciaAvisoContactable(referencia.AvisoId, referencia.VendedorId);
    }
}
