using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Detalle público de un aviso Activo. Devuelve null si no existe o no está Activo.</summary>
public sealed record ObtenerAvisoPublicoQuery(Guid Id) : IQuery<AvisoPublicoDto?>;

/// <summary>Handler de <see cref="ObtenerAvisoPublicoQuery"/>.</summary>
public sealed class ObtenerAvisoPublicoQueryHandler(IConsultaAvisosPublica consulta)
    : IQueryHandler<ObtenerAvisoPublicoQuery, AvisoPublicoDto?>
{
    public Task<AvisoPublicoDto?> Handle(ObtenerAvisoPublicoQuery request, CancellationToken cancellationToken) =>
        consulta.ObtenerPublicoAsync(request.Id, cancellationToken);
}
