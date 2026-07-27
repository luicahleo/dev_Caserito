using System.Net;
using System.Net.Http.Json;
using CaseritoApp.IntegrationTests.Infrastructure;
using CaseritoApp.Notifications.Application.PuntosEncuentro;
using CaseritoApp.Notifications.Domain.PuntosEncuentro;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class PuntosEncuentroEndpointsTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Get_puntos_encuentro_por_ciudad_devuelve_solo_activos()
    {
        await factory.Services.SembrarPuntosEncuentroSegurosAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var inactivo = PuntoEncuentroSeguro.Crear(
            "Punto Inactivo",
            "cochabamba",
            "Dirección inactiva",
            activo: false).Valor;
        var otro = PuntoEncuentroSeguro.Crear(
            "Punto Santa Cruz",
            "santa cruz",
            "Dirección SC").Valor;
        db.PuntosEncuentroSeguros.Add(inactivo);
        db.PuntosEncuentroSeguros.Add(otro);
        await db.SaveChangesAsync();

        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/publico/puntos-encuentro?ciudad=Cochabamba");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var puntos = await respuesta.Content.ReadFromJsonAsync<List<PuntoEncuentroSeguroDto>>();
        Assert.NotNull(puntos);
        Assert.All(puntos!, p => Assert.True(p.Activo));
        Assert.All(puntos, p => Assert.Equal("cochabamba", p.Ciudad));
        Assert.DoesNotContain(puntos, p => p.Nombre == "Punto Inactivo");
        Assert.DoesNotContain(puntos, p => p.Nombre == "Punto Santa Cruz");
        Assert.Contains(puntos, p => p.Nombre == "Edificio Farmacia Boliviana");
    }

    [Fact]
    public async Task Get_puntos_encuentro_ciudad_sin_resultados_devuelve_lista_vacia()
    {
        await factory.Services.SembrarPuntosEncuentroSegurosAsync();

        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/publico/puntos-encuentro?ciudad=LaPaz");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var puntos = await respuesta.Content.ReadFromJsonAsync<List<PuntoEncuentroSeguroDto>>();
        Assert.NotNull(puntos);
        Assert.Empty(puntos!);
    }

    [Fact]
    public async Task Get_puntos_encuentro_sin_ciudad_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/publico/puntos-encuentro");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
