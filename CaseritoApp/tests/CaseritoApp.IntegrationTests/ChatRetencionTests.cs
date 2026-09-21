using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatRetencionTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task El_vendedor_no_percibe_la_conversacion_retenida()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        // El vendedor no la ve por ninguna vía.
        var listadoVendedor = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.DoesNotContain(listadoVendedor.Items, c => c.Id == conversacion.Id);
        Assert.Equal(0, await consulta.ContarNoLeidosAsync(vendedor, default));
        Assert.False(await consulta.PuedeAccederAsync(conversacion.Id, vendedor, default));
        Assert.False(await consulta.PuedeRecibirTiempoRealAsync(conversacion.Id, vendedor, default));

        // El comprador sí.
        var listadoComprador = await consulta.ListarAsync(comprador, null, 20, default);
        Assert.Contains(listadoComprador.Items, c => c.Id == conversacion.Id);
        Assert.True(await consulta.PuedeAccederAsync(conversacion.Id, comprador, default));
    }

    [Fact]
    public async Task Tras_liberarla_el_vendedor_la_ve_con_sus_mensajes()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        conversacion.LiberarPorVerificacion(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var listado = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.Contains(listado.Items, c => c.Id == conversacion.Id);
        Assert.Equal(1, await consulta.ContarNoLeidosAsync(vendedor, default));
    }
}
