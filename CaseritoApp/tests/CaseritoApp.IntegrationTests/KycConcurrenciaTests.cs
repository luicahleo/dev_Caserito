using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Concurrencia optimista del KYC: token incremental (int, IsConcurrencyToken) en la raíz.</summary>
public sealed class KycConcurrenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    // Mecanismo: dos aprobaciones concurrentes del mismo agregado → la segunda pierde la carrera.
    [Fact]
    public async Task Dos_aprobaciones_concurrentes_del_mismo_agregado_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();
        Guid solicitudId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            solicitudId = envio.Valor.Id;
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        verB.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    // Mecanismo: dos solicitudes nuevas concurrentes sobre un agregado existente (tras rechazo) →
    // la segunda pierde. Valida que "añadir una hija" también bumpea la raíz (touch-root).
    [Fact]
    public async Task Dos_solicitudes_concurrentes_sobre_agregado_existente_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            verificacion.Rechazar(envio.Valor.Id, Guid.NewGuid(), "ilegible", DateTimeOffset.UtcNow);
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.EnviarSolicitud("doc-a", "selfie-a", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        verB.EnviarSolicitud("doc-b", "selfie-b", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
