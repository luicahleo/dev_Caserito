using System.Net;
using CaseritoApp.Notifications.Application.Push;
using Microsoft.Extensions.Options;
using WebPush;

namespace CaseritoApp.Notifications.Infrastructure.Push;

public sealed class OpcionesWebPush
{
    public const string Seccion = "WebPush";
    public bool Habilitado { get; init; }
    public string ClavePublica { get; init; } = string.Empty;
    public string ClavePrivada { get; init; } = string.Empty;
    public string Sujeto { get; init; } = string.Empty;
}

public sealed class WebPushSender(IOptions<OpcionesWebPush> opciones) : IWebPushSender
{
    private readonly OpcionesWebPush _opciones = opciones.Value;

    public async Task<ResultadoEnvioPush> EnviarAsync(EnvioPush envio, CancellationToken ct)
    {
        if (!_opciones.Habilitado)
        {
            return ResultadoEnvioPush.ErrorTransitorio;
        }

        using var cliente = new WebPushClient();
        try
        {
            await cliente.SendNotificationAsync(
                new PushSubscription(envio.Endpoint, envio.P256dh, envio.Auth),
                envio.Payload,
                new VapidDetails(_opciones.Sujeto, _opciones.ClavePublica, _opciones.ClavePrivada),
                ct);
            return ResultadoEnvioPush.Exito;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            return ResultadoEnvioPush.SuscripcionExpirada;
        }
        catch (WebPushException)
        {
            return ResultadoEnvioPush.ErrorTransitorio;
        }
    }
}
