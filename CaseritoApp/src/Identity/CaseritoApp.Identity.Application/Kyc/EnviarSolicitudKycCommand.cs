using CaseritoApp.BuildingBlocks.Application.Messaging;
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
/// Handler: guarda ambos blobs cifrados, crea/actualiza el agregado y registra la solicitud. Si el
/// dominio rechaza el envío, borra los blobs recién escritos (compensación best-effort).
/// </summary>
public sealed partial class EnviarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    TimeProvider tiempo,
    ILogger<EnviarSolicitudKycCommandHandler> logger)
    : ICommandHandler<EnviarSolicitudKycCommand>
{
    public async Task<Result> Handle(EnviarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorUsuarioAsync(request.UsuarioId, cancellationToken);
        var esNueva = verificacion is null;
        verificacion ??= VerificacionKyc.Crear(request.UsuarioId);

        var claveDoc = await almacen.GuardarAsync(request.Documento, request.DocumentoContentType, cancellationToken);
        var claveSelfie = await almacen.GuardarAsync(request.Selfie, request.SelfieContentType, cancellationToken);

        var resultado = verificacion.EnviarSolicitud(
            claveDoc, claveSelfie, TipoDocumento.CedulaIdentidad, tiempo.GetUtcNow());

        if (!resultado.EsExito)
        {
            // Compensación: los blobs quedaron escritos pero la solicitud no se creó.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resultado.Error.Code);
            return Result.Fallo(resultado.Error);
        }

        if (esNueva)
        {
            repositorio.Agregar(verificacion);
        }

        RegistrarEnvio(logger, request.UsuarioId, "ok");
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
