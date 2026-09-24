using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Reacciona a <see cref="SolicitudKycEnEspera"/> avisando por correo al buzón de
/// administración. Un fallo del relay SMTP se loguea pero no se propaga: la solicitud ya
/// quedó persistida y el contador del panel sigue siendo la fuente de verdad.
/// </summary>
public sealed partial class NotificarSolicitudKycEnEsperaHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IOpcionesAvisosKyc opcionesAvisos,
    OpcionesApp opcionesApp,
    ILogger<NotificarSolicitudKycEnEsperaHandler> logger)
    : IDomainEventConsumer<SolicitudKycEnEspera>
{
    public async Task Handle(SolicitudKycEnEspera evento, CancellationToken cancellationToken)
    {
        var buzon = opcionesAvisos.EmailAvisos;
        if (string.IsNullOrWhiteSpace(buzon))
        {
            RegistrarSinBuzon(logger);
            return;
        }

        var urlPanel = $"{opcionesApp.UrlPublica.TrimEnd('/')}/admin/kyc";
        var mensaje = new MensajeCorreo(
            buzon,
            plantilla.AsuntoSolicitudKycEnEspera(),
            plantilla.CuerpoSolicitudKycEnEspera(urlPanel));

        try
        {
            await servicioCorreo.EnviarAsync(mensaje, cancellationToken);
            RegistrarAviso(logger, evento.SolicitudId);
        }
        catch (Exception ex)
        {
            RegistrarFalloEnvio(logger, evento.SolicitudId, ex);
        }
    }

    // El buzón configurado nunca se registra: es un dato de configuración, no de diagnóstico.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Aviso de KYC en espera omitido: no hay buzon configurado")]
    private static partial void RegistrarSinBuzon(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Aviso de KYC en espera enviado: solicitud={SolicitudId}")]
    private static partial void RegistrarAviso(ILogger logger, Guid solicitudId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el envio del aviso de KYC en espera: solicitud={SolicitudId}")]
    private static partial void RegistrarFalloEnvio(ILogger logger, Guid solicitudId, Exception ex);
}
