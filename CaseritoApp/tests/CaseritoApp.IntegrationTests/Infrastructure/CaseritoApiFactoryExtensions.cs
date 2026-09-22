using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests.Infrastructure;

public static class CaseritoApiFactoryExtensions
{
    /// <summary>Devuelve una factory con ARGOS mockeado para aprobar KYC automáticamente.</summary>
    public static WebApplicationFactory<Program> ConAprobadorArgos(this CaseritoApiFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ => new VerificadorArgosAprobador());
        }));

    /// <summary>
    /// Marca al usuario como habilitado para el marketplace aprobando un KYC de prueba directamente
    /// en la BD. Se usa en tests de chat que necesitan una conversación Activa sin pasar por ARGOS.
    /// </summary>
    public static async Task AprobarKycAsync(this CaseritoApiFactory factory, Guid usuarioId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var verificacion = VerificacionKyc.Crear(usuarioId);
        var solicitud = verificacion.EnviarSolicitud(
            "doc-prueba", "selfie-prueba", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow).Valor;
        verificacion.Aprobar(solicitud.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.VerificacionesKyc.Add(verificacion);
        await db.SaveChangesAsync();
    }
}
