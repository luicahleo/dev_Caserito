using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Notifications.Application.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Host.Notifications;

public sealed class ConsultaProductoParaAlertaAdapter(CatalogDbContext db)
    : IConsultaProductoParaAlerta
{
    public async Task<ProductoAlertaDto?> ObtenerAsync(Guid avisoId, CancellationToken ct)
    {
        var aviso = await db.Avisos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == avisoId, ct);

        if (aviso is null)
        {
            return null;
        }

        var categoria = await db.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == aviso.CategoriaId, ct);
        var ciudad = await db.Ciudades
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == aviso.CiudadId, ct);

        return new ProductoAlertaDto(
            aviso.Id,
            aviso.Titulo,
            categoria?.Nombre,
            ciudad?.Nombre,
            aviso.Precio?.Monto,
            aviso.Condicion.ToString());
    }
}
