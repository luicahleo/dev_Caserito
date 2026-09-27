using CaseritoApp.Notifications.Application.Push;
using CaseritoApp.Notifications.Infrastructure.Push;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class WebPushInfrastructureTests
{
    [Fact]
    public void Comprobante_valido_se_recupera_y_entradas_invalidas_se_rechazan()
    {
        var protector = new ProtectorComprobantesEntrega(
            new EphemeralDataProtectionProvider());
        var esperado = new DatosComprobanteEntrega(Guid.NewGuid(), Guid.NewGuid(), 7);
        var comprobante = protector.Crear(esperado, DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True(protector.TryValidar(comprobante, out var recuperado));
        Assert.Equal(esperado, recuperado);
        Assert.False(protector.TryValidar(" ", out _));
        Assert.False(protector.TryValidar(new string('x', 4097), out _));
        Assert.False(protector.TryValidar(comprobante + "alterado", out _));
    }

    [Fact]
    public void Comprobante_rechaza_payload_con_campos_invalidos()
    {
        var protector = new ProtectorComprobantesEntrega(
            new EphemeralDataProtectionProvider());

        foreach (var datos in new[]
                 {
                     new DatosComprobanteEntrega(Guid.Empty, Guid.NewGuid(), 1),
                     new DatosComprobanteEntrega(Guid.NewGuid(), Guid.Empty, 1),
                     new DatosComprobanteEntrega(Guid.NewGuid(), Guid.NewGuid(), 0),
                 })
        {
            var comprobante = protector.Crear(datos, DateTimeOffset.UtcNow.AddMinutes(5));
            Assert.False(protector.TryValidar(comprobante, out _));
        }
    }

    [Fact]
    public async Task Sender_deshabilitado_devuelve_error_transitorio_sin_red()
    {
        var sender = new WebPushSender(Options.Create(new OpcionesWebPush { Habilitado = false }));

        var resultado = await sender.EnviarAsync(
            new EnvioPush("https://push.example.test", "clave", "auth", "{}"), default);

        Assert.Equal(ResultadoEnvioPush.ErrorTransitorio, resultado);
    }
}
