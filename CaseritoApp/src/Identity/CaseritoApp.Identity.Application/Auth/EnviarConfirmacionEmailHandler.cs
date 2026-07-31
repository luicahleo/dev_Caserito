using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Auth;

public sealed partial class EnviarConfirmacionEmailHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IGeneradorTokenEmail generadorToken,
    OpcionesApp opcionesApp,
    ILogger<EnviarConfirmacionEmailHandler> logger)
    : IDomainEventConsumer<UsuarioRegistrado>
{
    public async Task Handle(UsuarioRegistrado evento, CancellationToken cancellationToken)
    {
        var token = generadorToken.Generar(evento.UsuarioId);
        var urlBase = opcionesApp.UrlPublica.TrimEnd('/');
        var url = $"{urlBase}/confirmar-email?userId={evento.UsuarioId}&token={Uri.EscapeDataString(token)}";

        var mensaje = new MensajeCorreo(
            evento.Email,
            plantilla.AsuntoConfirmacionEmail(evento.Nombre),
            plantilla.CuerpoConfirmacionEmail(evento.Nombre, url));

        try
        {
            await servicioCorreo.EnviarAsync(mensaje, cancellationToken);
            RegistrarEnvio(logger, evento.UsuarioId);
        }
        catch (Exception ex)
        {
            RegistrarFalloEnvio(logger, evento.UsuarioId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo de confirmacion enviado: usuario={UsuarioId}")]
    private static partial void RegistrarEnvio(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el envio de correo de confirmacion: usuario={UsuarioId}")]
    private static partial void RegistrarFalloEnvio(ILogger logger, Guid usuarioId, Exception ex);
}
