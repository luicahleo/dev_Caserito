using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Fotos;

internal sealed class ConsultaFotoPublicaEfCore(CatalogDbContext db) : IConsultaFotoPublica
{
    public Task<bool> EsPublicaAsync(string clave, CancellationToken ct) =>
        db.Avisos.AsNoTracking().AnyAsync(
            aviso => aviso.Estado == EstadoAviso.Activo
                && aviso.EstadoModeracion == EstadoModeracionAviso.Visible
                && aviso.Fotos.Any(foto => foto.Clave == clave),
            ct);
}
