using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Notifications.Application.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Host.Notifications;

public sealed class ConsultaProductoParaAlertaAdapter(CatalogDbContext db)
    : IConsultaProductoParaAlerta
{
    public async Task<ProductoAlertaDto?> ObtenerAsync(Guid avisoId, CancellationToken ct)
    {
        var dto = await (
            from a in db.Avisos.AsNoTracking()
            where a.Id == avisoId
            join c in db.Categorias.AsNoTracking() on a.CategoriaId equals c.Id into categorias
            from c in categorias.DefaultIfEmpty()
            join ci in db.Ciudades.AsNoTracking() on a.CiudadId equals ci.Id into ciudades
            from ci in ciudades.DefaultIfEmpty()
            select new ProductoAlertaDto(
                a.Id,
                a.Titulo,
                c == null ? null : c.Nombre,
                ci == null ? null : ci.Nombre,
                a.Precio == null ? null : a.Precio.Monto,
                a.Condicion.ToString()))
            .FirstOrDefaultAsync(ct);

        return dto;
    }
}
