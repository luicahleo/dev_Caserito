using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Envía una solicitud de verificación con documento (CI) y selfie del usuario autenticado.</summary>
public sealed record EnviarSolicitudKycCommand(
    Guid UsuarioId,
    byte[] Documento,
    string DocumentoContentType,
    byte[] Selfie,
    string SelfieContentType) : ICommand;

/// <summary>
/// Handler: valida invariants, guarda los blobs cifrados, consulta a ARGOS y aplica la decisión
/// automática (aprobación/rechazo). Si ARGOS falla, compensa eliminando los blobs. Solo publica
/// <see cref="UserVerified"/> cuando la solicitud es aprobada automáticamente.
/// </summary>
public sealed partial class EnviarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    IVerificadorIdentidadArgos verificador,
    IPublicadorEventosIntegracion publicador,
    TimeProvider tiempo,
    ILogger<EnviarSolicitudKycCommandHandler> logger)
    : ICommandHandler<EnviarSolicitudKycCommand>
{
    public async Task<Result> Handle(EnviarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorUsuarioAsync(request.UsuarioId, cancellationToken);
        var esNueva = verificacion is null;
        verificacion ??= VerificacionKyc.Crear(request.UsuarioId);

        // Validar invariants antes de tocar blobs o llamar a ARGOS.
        var validacionPrevia = verificacion.PuedeEnviarSolicitud();
        if (!validacionPrevia.EsExito)
        {
            RegistrarEnvio(logger, request.UsuarioId, validacionPrevia.Error.Code);
            return Result.Fallo(validacionPrevia.Error);
        }

        var claveDoc = await almacen.GuardarAsync(request.Documento, request.DocumentoContentType, cancellationToken);
        var claveSelfie = await almacen.GuardarAsync(request.Selfie, request.SelfieContentType, cancellationToken);

        var resultadoArgos = await verificador.VerificarAsync(request.Documento, request.Selfie, cancellationToken);

        if (!resultadoArgos.EsExito)
        {
            // Compensación: ARGOS no responde, eliminar blobs y no persistir nada.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resultadoArgos.Error.Code);
            return Result.Fallo(resultadoArgos.Error);
        }

        var facial = resultadoArgos.Valor;

        var resultado = verificacion.EnviarSolicitud(
            claveDoc, claveSelfie, TipoDocumento.CedulaIdentidad, tiempo.GetUtcNow());

        if (!resultado.EsExito)
        {
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resultado.Error.Code);
            return Result.Fallo(resultado.Error);
        }

        var solicitud = resultado.Valor;
        solicitud.RegistrarScoreSimilitud(facial.SimilitudPercent);

        var ahora = tiempo.GetUtcNow();
        var resolucion = facial.Coinciden
            ? verificacion.Aprobar(solicitud.Id, SistemaActor.Id, ahora)
            : verificacion.Rechazar(solicitud.Id, SistemaActor.Id, facial.MotivoRechazo ?? "El rostro no coincide con el documento.", ahora);

        if (!resolucion.EsExito)
        {
            // Estado inconsistente: esto no debería ocurrir porque acabamos de crear la solicitud.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resolucion.Error.Code);
            return Result.Fallo(resolucion.Error);
        }

        if (facial.Coinciden)
        {
            await publicador.PublicarAsync(
                new UserVerified(Guid.NewGuid(), ahora, verificacion.UsuarioId), cancellationToken);
        }

        if (esNueva)
        {
            repositorio.Agregar(verificacion);
        }

        RegistrarEnvio(logger, request.UsuarioId, facial.Coinciden ? "aprobado-automatico" : "rechazado-automatico");
        return Result.Exito();
    }

    // Auditoría sin PII: solo id de usuario y resultado; jamás bytes, content-type ni claves de blob.
    [LoggerMessage(Level = LogLevel.Information, Message = "Solicitud KYC enviada: usuario={UsuarioId} resultado={Resultado}")]
    private static partial void RegistrarEnvio(ILogger logger, Guid usuarioId, string resultado);
}

/// <summary>Valida tamaño, tipo y coherencia (magic bytes) de documento y selfie.</summary>
public sealed class EnviarSolicitudKycCommandValidator : AbstractValidator<EnviarSolicitudKycCommand>
{
    public EnviarSolicitudKycCommandValidator()
    {
        RuleFor(c => c)
            .Must(c => ValidacionImagenKyc.EsImagenValida(c.Documento, c.DocumentoContentType))
            .WithName("Documento")
            .WithMessage("El documento debe ser una imagen JPEG o PNG de hasta 5 MB.");

        RuleFor(c => c)
            .Must(c => ValidacionImagenKyc.EsImagenValida(c.Selfie, c.SelfieContentType))
            .WithName("Selfie")
            .WithMessage("La selfie debe ser una imagen JPEG o PNG de hasta 5 MB.");
    }
}
