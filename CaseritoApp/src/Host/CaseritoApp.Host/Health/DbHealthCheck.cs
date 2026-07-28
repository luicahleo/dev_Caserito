using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Orders.Infrastructure;
using CaseritoApp.Reputation.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CaseritoApp.Host.Health;

/// <summary>
/// Extensiones para registrar los health checks de las bases de datos del piloto.
/// Cuando no hay cadena de conexión configurada (p. ej. tests sin Testcontainers)
/// se devuelve un check trivialmente saludable para no romper el endpoint /health.
/// </summary>
public static class DbHealthCheck
{
    public static IHealthChecksBuilder AddCaseritoDbContextChecks(
        this IHealthChecksBuilder builder,
        string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder.AddCheck(
                "database",
                () => HealthCheckResult.Healthy("No hay cadena de conexión configurada."));
        }

        return builder
            .AddDbContextCheck<IdentityDbContext>("identity")
            .AddDbContextCheck<CatalogDbContext>("catalog")
            .AddDbContextCheck<ChatDbContext>("chat")
            .AddDbContextCheck<OrdersDbContext>("orders")
            .AddDbContextCheck<ReputationDbContext>("reputation")
            .AddDbContextCheck<NotificationsDbContext>("notifications");
    }
}
