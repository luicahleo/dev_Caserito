using CaseritoApp.Identity.Application.Kyc;
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
}
