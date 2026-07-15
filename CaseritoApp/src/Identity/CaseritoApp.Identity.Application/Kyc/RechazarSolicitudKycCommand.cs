using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Rechaza una solicitud pendiente con un motivo (visible al usuario).</summary>
public sealed record RechazarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId, string Motivo) : ICommand;

/// <summary>Handler: rechaza en el agregado. No publica evento.</summary>
public sealed partial class RechazarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    TimeProvider tiempo,
    ILogger<RechazarSolicitudKycCommandHandler> logger)
    : ICommandHandler<RechazarSolicitudKycCommand>
{
    public async Task<Result> Handle(RechazarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        if (verificacion is null)
        {
            RegistrarResolucion(logger, "rechazar", request.RevisorId, request.SolicitudId, ErroresKyc.SolicitudNoEncontrada);
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var resultado = verificacion.Rechazar(request.SolicitudId, request.RevisorId, request.Motivo, tiempo.GetUtcNow());

        RegistrarResolucion(
            logger, "rechazar", request.RevisorId, request.SolicitudId,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolución KYC {Accion}: revisor={RevisorId} solicitud={SolicitudId} resultado={Resultado}")]
    private static partial void RegistrarResolucion(
        ILogger logger, string accion, Guid revisorId, Guid solicitudId, string resultado);
}

/// <summary>Valida que el motivo de rechazo esté presente y sea de longitud razonable.</summary>
public sealed class RechazarSolicitudKycCommandValidator : AbstractValidator<RechazarSolicitudKycCommand>
{
    public RechazarSolicitudKycCommandValidator()
    {
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(500).WithMessage("El motivo no puede superar los 500 caracteres.");
    }
}
