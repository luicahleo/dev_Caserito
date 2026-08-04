using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class CatalogoReferenciaTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Ciudades_devuelve_las_diez_ciudades_bolivianas_en_orden()
    {
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/catalogo/ciudades");
        var ciudades = await respuesta.Content.ReadFromJsonAsync<CiudadDto[]>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(
            [
                "Cochabamba",
                "Santa Cruz de la Sierra",
                "La Paz",
                "El Alto",
                "Sucre",
                "Oruro",
                "Tarija",
                "Potosí",
                "Trinidad",
                "Cobija",
            ],
            ciudades?.Select(c => c.Nombre));
    }

    [Fact]
    public async Task Consulta_de_perfil_solo_acepta_una_ciudad_activa()
    {
        using var alcance = factory.Services.CreateScope();
        var consulta = alcance.ServiceProvider.GetRequiredService<IConsultaCiudadesPerfil>();

        Assert.True(await consulta.ExisteActivaAsync(
            new Guid("22222222-2222-2222-2222-000000000009"),
            CancellationToken.None));
        Assert.False(await consulta.ExisteActivaAsync(
            Guid.NewGuid(),
            CancellationToken.None));
    }
}
