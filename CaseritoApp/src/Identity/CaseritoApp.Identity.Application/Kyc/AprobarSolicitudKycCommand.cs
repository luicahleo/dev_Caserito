using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Aprueba una solicitud pendiente y publica <c>UserVerified</c>.</summary>
public sealed record AprobarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId) : ICommand;

/// <summary>Handler: aprueba en el agregado y, si tiene éxito, publica el evento de integración.</summary>
public sealed partial class AprobarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IPublicadorEventosIntegracion publicador,
    TimeProvider tiempo,
    ILogger<AprobarSolicitudKycCommandHandler> logger)
    : ICommandHandler<AprobarSolicitudKycCommand>
{
    public async Task<Result> Handle(AprobarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        if (verificacion is null)
        {
            RegistrarResolucion(logger, "aprobar", request.RevisorId, request.SolicitudId, ErroresKyc.SolicitudNoEncontrada);
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var ahora = tiempo.GetUtcNow();
        var resultado = verificacion.Aprobar(request.SolicitudId, request.RevisorId, ahora);

        if (resultado.EsExito)
        {
            await publicador.PublicarAsync(
                new UserVerified(Guid.NewGuid(), ahora, verificacion.UsuarioId), cancellationToken);
        }

        RegistrarResolucion(
            logger, "aprobar", request.RevisorId, request.SolicitudId,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: revisor, solicitud, resultado. Sin email ni datos del documento.
    [LoggerMessage(Level = LogLevel.Information, Message = "Resolución KYC {Accion}: revisor={RevisorId} solicitud={SolicitudId} resultado={Resultado}")]
    private static partial void RegistrarResolucion(
        ILogger logger, string accion, Guid revisorId, Guid solicitudId, string resultado);
}
