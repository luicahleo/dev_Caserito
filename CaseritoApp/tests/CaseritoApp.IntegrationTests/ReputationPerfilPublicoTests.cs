using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ReputationPerfilPublicoTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Perfil_existente_es_minimo_y_sin_resenas_tiene_resumen_vacio()
    {
        var id = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var resultado = await usuarios.CreateAsync(new ApplicationUser
            {
                Id = id,
                UserName = $"publico-{id:N}@caserito.test",
                Email = $"publico-{id:N}@caserito.test",
                Nombre = "María",
                Ciudad = "Sucre",
            }, "Password123!");
            Assert.True(resultado.Succeeded);
        }

        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/api/publico/usuarios/{id}");
        var json = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("María", documento.RootElement.GetProperty("nombre").GetString());
        Assert.Equal("Sucre", documento.RootElement.GetProperty("ciudad").GetString());
        Assert.Equal(JsonValueKind.Null, documento.RootElement.GetProperty("promedio").ValueKind);
        Assert.Equal(0, documento.RootElement.GetProperty("totalResenas").GetInt32());
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permisos", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kyc", json, StringComparison.OrdinalIgnoreCase);

        var resenas = await cliente.GetFromJsonAsync<PaginaResenas>(
            $"/api/publico/usuarios/{id}/resenas");
        Assert.Empty(resenas!.Items);
        Assert.Equal(0, resenas.Total);
    }

    [Fact]
    public async Task Usuario_inexistente_devuelve_404_generico()
    {
        using var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync(
            $"/api/publico/usuarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var contenido = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", contenido, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PaginaResenas(
        JsonElement[] Items,
        int Pagina,
        int Tamano,
        int Total);
}
