using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>Seeding idempotente de categorías y ciudades de referencia del MVP.</summary>
public static class SeedCatalogoExtensions
{
    private static readonly (Guid Id, string Nombre, int Orden)[] _categorias =
    [
        (new("11111111-1111-1111-1111-000000000001"), "Electrónica", 1),
        (new("11111111-1111-1111-1111-000000000002"), "Vehículos", 2),
        (new("11111111-1111-1111-1111-000000000003"), "Hogar y muebles", 3),
        (new("11111111-1111-1111-1111-000000000004"), "Moda", 4),
        (new("11111111-1111-1111-1111-000000000005"), "Deportes", 5),
        (new("11111111-1111-1111-1111-000000000006"), "Mascotas", 6),
        (new("11111111-1111-1111-1111-000000000007"), "Bebés y niños", 7),
        (new("11111111-1111-1111-1111-000000000008"), "Libros y música", 8),
        (new("11111111-1111-1111-1111-000000000009"), "Servicios", 9),
        (new("11111111-1111-1111-1111-00000000000a"), "Otros", 10),
    ];

    private static readonly (Guid Id, string Nombre, int Orden)[] _ciudades =
    [
        (new("22222222-2222-2222-2222-000000000001"), "Cochabamba", 1),
        (new("22222222-2222-2222-2222-000000000002"), "Santa Cruz de la Sierra", 2),
        (new("22222222-2222-2222-2222-000000000003"), "La Paz", 3),
        (new("22222222-2222-2222-2222-000000000004"), "El Alto", 4),
        (new("22222222-2222-2222-2222-000000000005"), "Sucre", 5),
        (new("22222222-2222-2222-2222-000000000006"), "Oruro", 6),
        (new("22222222-2222-2222-2222-000000000007"), "Tarija", 7),
        (new("22222222-2222-2222-2222-000000000008"), "Potosí", 8),
        (new("22222222-2222-2222-2222-000000000009"), "Trinidad", 9),
        (new("22222222-2222-2222-2222-00000000000a"), "Cobija", 10),
    ];

    /// <summary>
    /// Asegura que existan las categorías y ciudades del MVP (por id fijo). Idempotente: re-ejecutar
    /// no crea duplicados. Debe llamarse DESPUÉS de aplicar las migraciones.
    /// </summary>
    public static async Task SembrarCatalogoAsync(this IServiceProvider proveedor, CancellationToken ct = default)
    {
        using var scope = proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        foreach (var (id, nombre, orden) in _categorias)
        {
            if (!await db.Categorias.AnyAsync(c => c.Id == id, ct))
            {
                db.Categorias.Add(Categoria.Crear(id, nombre, orden));
            }
        }

        foreach (var (id, nombre, orden) in _ciudades)
        {
            if (!await db.Ciudades.AnyAsync(c => c.Id == id, ct))
            {
                db.Ciudades.Add(Ciudad.Crear(id, nombre, orden));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
