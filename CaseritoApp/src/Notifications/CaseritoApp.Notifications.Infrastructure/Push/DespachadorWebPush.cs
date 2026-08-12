using System.Text.Json;
using CaseritoApp.Notifications.Application.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.Notifications.Infrastructure.Push;

public sealed class DespachadorWebPush(
    IServiceScopeFactory scopes,
    TimeProvider reloj) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(10), reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            await ProcesarLoteAsync(stoppingToken);
        }
    }

    public async Task ProcesarLoteAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var almacen = scope.ServiceProvider.GetRequiredService<IAlmacenIntencionesPush>();
        var suscripciones = scope.ServiceProvider.GetRequiredService<IRepositorioSuscripcionesPush>();
        var sender = scope.ServiceProvider.GetRequiredService<IWebPushSender>();
        var protector = scope.ServiceProvider.GetRequiredService<IProtectorComprobantesEntrega>();
        var ahora = reloj.GetUtcNow();
        var lote = await almacen.ReclamarAsync(20, ahora, TimeSpan.FromMinutes(1), ct);
        await db.SaveChangesAsync(ct);

        foreach (var intencion in lote)
        {
            var activas = await suscripciones.ListarActivasAsync(intencion.DestinatarioId, ct);
            var reintentar = false;
            foreach (var suscripcion in activas)
            {
                var comprobante = protector.Crear(
                    new DatosComprobanteEntrega(
                        intencion.ConversacionId, intencion.DestinatarioId, intencion.Secuencia),
                    ahora.AddHours(24));
                var payload = JsonSerializer.Serialize(new
                {
                    titulo = "Nuevo mensaje",
                    mensaje = "Tienes un nuevo mensaje",
                    ruta = $"/mensajes/{intencion.ConversacionId}",
                    comprobante
                });
                var resultado = await sender.EnviarAsync(new EnvioPush(
                    suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth, payload), ct);
                if (resultado == ResultadoEnvioPush.SuscripcionExpirada)
                {
                    suscripcion.Desactivar(ahora);
                }
                else if (resultado == ResultadoEnvioPush.ErrorTransitorio)
                {
                    reintentar = true;
                }
            }

            if (reintentar)
            {
                var segundos = Math.Min(3600, 30 * Math.Pow(2, intencion.Intentos));
                intencion.Reprogramar(ahora, TimeSpan.FromSeconds(segundos));
            }
            else
            {
                intencion.MarcarProcesada(ahora);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
        }
    }
}
