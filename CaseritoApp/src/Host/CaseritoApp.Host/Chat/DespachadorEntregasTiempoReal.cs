using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Infrastructure.TiempoReal;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Host.Chat;

public sealed class DespachadorEntregasTiempoReal(
    IServiceScopeFactory scopes,
    IOptions<OpcionesTiempoRealChat> opciones) : BackgroundService
{
    private readonly OpcionesTiempoRealChat _opciones = opciones.Value;

    public async Task ProcesarLoteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var almacen = scope.ServiceProvider.GetRequiredService<IAlmacenEntregasTiempoReal>();
        var publicador = scope.ServiceProvider.GetRequiredService<IPublicadorMensajesTiempoReal>();
        var conversaciones = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();
        var entregas = await almacen.ReclamarAsync(
            _opciones.TamanoLote,
            TimeSpan.FromSeconds(_opciones.SegundosLease),
            cancellationToken);

        foreach (var entrega in entregas)
        {
            try
            {
                var mensaje = entrega.Mensaje;
                var elegible = await conversaciones.PuedeRecibirTiempoRealAsync(
                    mensaje.ConversacionId,
                    mensaje.RemitenteId,
                    cancellationToken);
                if (!elegible)
                {
                    await almacen.MarcarProcesadaAsync(entrega, cancellationToken);
                    continue;
                }

                await publicador.PublicarAsync(
                    new MensajeTiempoRealDto(
                        mensaje.Id,
                        mensaje.ConversacionId,
                        mensaje.RemitenteId,
                        mensaje.Secuencia,
                        mensaje.Texto,
                        mensaje.EnviadoEn),
                    cancellationToken);
                await almacen.MarcarProcesadaAsync(entrega, cancellationToken);
            }
#pragma warning disable S2221 // El límite de red puede fallar con excepciones de proveedor; se reprograman sin registrar datos.
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore S2221
            {
                await almacen.ReprogramarAsync(entrega, cancellationToken);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarLoteAsync(stoppingToken);
                await Task.Delay(_opciones.MilisegundosSondeo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable S2221 // El servicio debe sobrevivir a fallos técnicos sin registrar excepciones potencialmente sensibles.
            catch (Exception)
#pragma warning restore S2221
            {
                await Task.Delay(_opciones.MilisegundosSondeo, stoppingToken);
            }
        }
    }
}
