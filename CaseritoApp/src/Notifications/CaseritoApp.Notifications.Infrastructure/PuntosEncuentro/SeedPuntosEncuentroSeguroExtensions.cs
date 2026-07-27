using CaseritoApp.Notifications.Domain.PuntosEncuentro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;

/// <summary>Seeding idempotente de puntos de encuentro seguros del MVP.</summary>
public static class SeedPuntosEncuentroSeguroExtensions
{
    private static readonly (Guid Id, string Nombre, string Ciudad, string Direccion)[] _puntosCochabamba =
    [
        (
            new Guid("33333333-3333-3333-3333-000000000001"),
            "Edificio Farmacia Boliviana",
            "cochabamba",
            "Calle España esq. Ayacucho"),
        (
            new Guid("33333333-3333-3333-3333-000000000002"),
            "Catedral San Sebastián",
            "cochabamba",
            "Plaza 14 de Septiembre"),
        (
            new Guid("33333333-3333-3333-3333-000000000003"),
            "Supermercado Ketal Calle Sucre",
            "cochabamba",
            "Calle Sucre Nº 567"),
        (
            new Guid("33333333-3333-3333-3333-000000000004"),
            "Cine Center Cochabamba",
            "cochabamba",
            "Av. América Nº 1234"),
        (
            new Guid("33333333-3333-3333-3333-000000000005"),
            "UMSS Campus Central",
            "cochabamba",
            "Av. Oquendo y Jordán"),
    ];

    /// <summary>
    /// Asegura que existan los puntos de encuentro seguros del MVP (por id fijo). Idempotente:
    /// re-ejecutar no crea duplicados. Debe llamarse DESPUÉS de aplicar las migraciones.
    /// </summary>
    public static async Task SembrarPuntosEncuentroSegurosAsync(
        this IServiceProvider proveedor,
        CancellationToken ct = default)
    {
        using var scope = proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        foreach (var (id, nombre, ciudad, direccion) in _puntosCochabamba)
        {
            if (!await db.PuntosEncuentroSeguros.AnyAsync(p => p.Id == id, ct))
            {
                var punto = PuntoEncuentroSeguro.Crear(nombre, ciudad, direccion, id: id).Valor;
                db.PuntosEncuentroSeguros.Add(punto);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
