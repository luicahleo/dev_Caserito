using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using CaseritoApp.Host.Chat;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatTiempoRealTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Solo_comprador_y_vendedor_pueden_acceder_a_la_conversacion()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        await db.SaveChangesAsync();
        var consulta = new ConsultaConversacionesEfCore(db);

        Assert.True(await consulta.PuedeAccederAsync(
            conversacion.Id, conversacion.CompradorId, CancellationToken.None));
        Assert.True(await consulta.PuedeAccederAsync(
            conversacion.Id, conversacion.VendedorId, CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            conversacion.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            Guid.NewGuid(), conversacion.CompradorId, CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            Guid.Empty, conversacion.CompradorId, CancellationToken.None));
    }

    [Fact]
    public void Grupo_de_conversacion_es_determinista_invariante_y_no_acepta_nombres()
    {
        var id = Guid.Parse("A48B63CD-4D4A-44E8-A3E2-D4F6A9E16051");

        var grupo = GruposChat.ParaConversacion(id);

        Assert.Equal("chat:conversacion:a48b63cd4d4a44e8a3e2d4f6a9e16051", grupo);
        Assert.Single(typeof(GruposChat).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
    }
}
