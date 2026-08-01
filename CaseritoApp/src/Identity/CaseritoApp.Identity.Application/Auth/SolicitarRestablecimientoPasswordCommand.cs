using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Correo;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Solicita un correo de restablecimiento sin revelar si la cuenta existe.</summary>
public sealed record SolicitarRestablecimientoPasswordCommand(string Email) : ICommand;

/// <summary>Handler anti-enumeración para solicitar el enlace de restablecimiento.</summary>
public sealed partial class SolicitarRestablecimientoPasswordCommandHandler(
    IRepositorioRestablecimientoPassword repositorio,
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    OpcionesApp opcionesApp,
    ILogger<SolicitarRestablecimientoPasswordCommandHandler> logger)
    : ICommandHandler<SolicitarRestablecimientoPasswordCommand>
{
    public async Task<Result> Handle(
        SolicitarRestablecimientoPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var solicitud = await repositorio.CrearSolicitudAsync(request.Email, cancellationToken);
        if (solicitud is null)
        {
            return Result.Exito();
        }

        var urlBase = opcionesApp.UrlPublica.TrimEnd('/');
        var usuarioId = Uri.EscapeDataString(solicitud.UsuarioId.ToString());
        var token = Uri.EscapeDataString(solicitud.Token);
        var url = $"{urlBase}/restablecer-password#usuarioId={usuarioId}&token={token}";
        var mensaje = new MensajeCorreo(
            solicitud.Email,
            plantilla.AsuntoRestablecimientoPassword(solicitud.Nombre),
            plantilla.CuerpoRestablecimientoPassword(solicitud.Nombre, url));

        try
        {
            await servicioCorreo.EnviarAsync(mensaje, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            RegistrarFalloEnvio(logger);
        }

        return Result.Exito();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Fallo el envío del correo de restablecimiento de contraseña")]
    private static partial void RegistrarFalloEnvio(ILogger logger);
}
