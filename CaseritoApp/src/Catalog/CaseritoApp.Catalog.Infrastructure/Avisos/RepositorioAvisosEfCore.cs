using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Adaptador EF Core de <see cref="IRepositorioAvisos"/>.</summary>
public sealed class RepositorioAvisosEfCore(CatalogDbContext db) : IRepositorioAvisos
{
    public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Avisos.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct) =>
        db.Avisos
          .Include(a => a.Fotos)
          .FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Agregar(Aviso aviso) => db.Avisos.Add(aviso);

    public async Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
        Guid vendedorId, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.Avisos
            .Where(a => a.VendedorId == vendedorId && a.Estado != EstadoAviso.Eliminado);

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(a => a.FechaCreacion)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(a => new AvisoResumenDto(
                a.Id,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                a.CategoriaId,
                a.CiudadId,
                a.Condicion.ToString(),
                a.Estado.ToString(),
                a.FechaCreacion,
                a.Fotos
                  .OrderBy(f => f.Orden)
                  .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
                  .ToList()))
            .ToListAsync(ct);

        return new ResultadoPaginado<AvisoResumenDto>(items, pagina, tamano, total);
    }
}
