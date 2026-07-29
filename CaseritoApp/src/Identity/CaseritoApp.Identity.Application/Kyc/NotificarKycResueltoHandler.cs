using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Reacciona a <see cref="KycResuelto"/> enviando al usuario el correo de aprobación o
/// rechazo. Un fallo del relay SMTP se loguea pero no se propaga: la resolución del KYC
/// ya quedó persistida y el correo puede reenviarse manualmente.
/// </summary>
public sealed partial class NotificarKycResueltoHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IConsultaVerificacionKyc consulta,
    ILogger<NotificarKycResueltoHandler> logger)
    : IDomainEventConsumer<KycResuelto>
{
    public async Task Handle(KycResuelto evento, CancellationToken cancellationToken)
    {
        var usuario = await consulta.ObtenerUsuarioAsync(evento.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            RegistrarUsuarioNoEncontrado(logger, evento.UsuarioId);
            return;
        }

        MensajeCorreo? mensaje = evento.Estado switch
        {
            EstadoKyc.Aprobada => new MensajeCorreo(
                usuario.Email,
                plantilla.AsuntoKycAprobado(usuario.Nombre),
                plantilla.CuerpoKycAprobado(usuario.Nombre)),
            EstadoKyc.Rechazada => new MensajeCorreo(
                usuario.Email,
                plantilla.AsuntoKycRechazado(usuario.Nombre),
                plantilla.CuerpoKycRechazado(usuario.Nombre, evento.MotivoRechazo ?? "No especificado.")),
            _ => null,
        };

        if (mensaje is null)
        {
            return;
        }

        try
        {
            await servicioCorreo.EnviarAsync(mensaje, cancellationToken);
            RegistrarNotificacion(logger, evento.UsuarioId, evento.Estado);
        }
        catch (Exception ex)
        {
            RegistrarFalloEnvio(logger, evento.UsuarioId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usuario no encontrado al notificar KYC: usuario={UsuarioId}")]
    private static partial void RegistrarUsuarioNoEncontrado(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notificacion KYC enviada: usuario={UsuarioId} estado={Estado}")]
    private static partial void RegistrarNotificacion(ILogger logger, Guid usuarioId, EstadoKyc estado);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el envio de notificacion KYC: usuario={UsuarioId}")]
    private static partial void RegistrarFalloEnvio(ILogger logger, Guid usuarioId, Exception ex);
}
