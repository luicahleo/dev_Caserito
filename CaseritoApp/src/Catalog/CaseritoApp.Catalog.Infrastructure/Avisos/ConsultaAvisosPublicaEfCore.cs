using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>
/// Adaptador EF Core de <see cref="IConsultaAvisosPublica"/>. Solo consulta avisos Activos.
/// La búsqueda de texto usa LIKE con colación acento/mayúscula-insensible.
/// </summary>
public sealed class ConsultaAvisosPublicaEfCore(CatalogDbContext db) : IConsultaAvisosPublica
{
    // Insensible a mayúsculas (CI) y acentos (AI), independiente de la colación de la columna.
    private const string Colacion = "Latin1_General_CI_AI";

    public async Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
        FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.Avisos.Where(a =>
            a.Estado == EstadoAviso.Activo && a.EstadoModeracion == EstadoModeracionAviso.Visible);

        if (filtro.CategoriaId is { } categoria)
        {
            consulta = consulta.Where(a => a.CategoriaId == categoria);
        }

        if (filtro.CiudadId is { } ciudad)
        {
            consulta = consulta.Where(a => a.CiudadId == ciudad);
        }

        if (filtro.PrecioMin is { } min)
        {
            consulta = consulta.Where(a => a.Precio.Monto >= min);
        }

        if (filtro.PrecioMax is { } max)
        {
            consulta = consulta.Where(a => a.Precio.Monto <= max);
        }

        if (filtro.Condicion is { } condicion)
        {
            consulta = consulta.Where(a => a.Condicion == condicion);
        }

        foreach (var token in filtro.Tokens)
        {
            var patron = $"%{EscaparComodines(token)}%";
            consulta = consulta.Where(a =>
                EF.Functions.Like(EF.Functions.Collate(a.Titulo, Colacion), patron) ||
                EF.Functions.Like(EF.Functions.Collate(a.Descripcion, Colacion), patron));
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(a => a.FechaCreacion)
            .ThenBy(a => a.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(a => new AvisoPublicoResumenDto(
                a.Id,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                db.Categorias.Where(c => c.Id == a.CategoriaId).Select(c => c.Nombre).First(),
                db.Ciudades.Where(c => c.Id == a.CiudadId).Select(c => c.Nombre).First(),
                a.Condicion.ToString(),
                a.FechaCreacion,
                a.Fotos
                  .OrderBy(f => f.Orden)
                  .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
                  .ToList()))
            .ToListAsync(ct);

        return new ResultadoPaginado<AvisoPublicoResumenDto>(items, pagina, tamano, total);
    }

    public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
        db.Avisos
            .Where(a => a.Id == id && a.Estado == EstadoAviso.Activo &&
                a.EstadoModeracion == EstadoModeracionAviso.Visible)
            .Select(a => new AvisoPublicoDto(
                a.Id,
                a.Titulo,
                a.Descripcion,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                db.Categorias.Where(c => c.Id == a.CategoriaId).Select(c => c.Nombre).First(),
                db.Ciudades.Where(c => c.Id == a.CiudadId).Select(c => c.Nombre).First(),
                a.Condicion.ToString(),
                a.FechaCreacion,
                a.Fotos
                  .OrderBy(f => f.Orden)
                  .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
                  .ToList()))
            .FirstOrDefaultAsync(ct);

    public Task<ReferenciaAvisoContactableDto?> ObtenerReferenciaContactableAsync(
        Guid id,
        CancellationToken ct) => db.Avisos
            .Where(a => a.Id == id &&
                a.Estado == EstadoAviso.Activo &&
                a.EstadoModeracion == EstadoModeracionAviso.Visible)
            .Select(a => new ReferenciaAvisoContactableDto(
                a.Id,
                a.VendedorId,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString()))
            .FirstOrDefaultAsync(ct);

    // Neutraliza los comodines de LIKE en el término del usuario usando clases de caracteres.
    private static string EscaparComodines(string token) =>
        token.Replace("[", "[[]", StringComparison.Ordinal)
             .Replace("%", "[%]", StringComparison.Ordinal)
             .Replace("_", "[_]", StringComparison.Ordinal);
}
