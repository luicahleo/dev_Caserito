using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Envía una solicitud de verificación con documento (CI) y selfie del usuario autenticado.</summary>
public sealed record EnviarSolicitudKycCommand(
    Guid UsuarioId,
    string NumeroCi,
    string? ComplementoCi,
    DepartamentoBolivia DepartamentoExpedicion,
    byte[] Documento,
    string DocumentoContentType,
    byte[] Selfie,
    string SelfieContentType) : ICommand
{
    public EnviarSolicitudKycCommand(
        Guid usuarioId, byte[] documento, string documentoContentType,
        byte[] selfie, string selfieContentType)
        : this(usuarioId, "1234567", null, DepartamentoBolivia.Cochabamba,
            documento, documentoContentType, selfie, selfieContentType)
    {
    }
}

/// <summary>
/// Handler: valida invariants, guarda los blobs cifrados, consulta a ARGOS y aplica la decisión
/// automática (aprobación/rechazo). Si ARGOS falla, compensa eliminando los blobs. Solo publica
/// <see cref="UserVerified"/> cuando la solicitud es aprobada automáticamente.
/// </summary>
public sealed partial class EnviarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    IVerificadorIdentidadArgos verificador,
    IProtectorDocumentoKyc protectorDocumento,
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

        var protegido = protectorDocumento.Proteger(
            request.NumeroCi, request.ComplementoCi, request.DepartamentoExpedicion);
        if (!await repositorio.ReservarDocumentoAsync(
            new DocumentoKycRegistrado(
                request.UsuarioId, protegido.HuellaCi, protegido.NumeroCiCifrado,
                protegido.ComplementoCiCifrado, protegido.DepartamentoExpedicion,
                tiempo.GetUtcNow()),
            cancellationToken))
        {
            return Result.Fallo(new Error("Kyc.DocumentoEnUso", "No se pudo registrar el documento."));
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
            ? Result.Exito()
            : verificacion.Rechazar(
                solicitud.Id,
                SistemaActor.Id,
                "La validación facial no fue satisfactoria.",
                ahora);

        if (!resolucion.EsExito)
        {
            // Estado inconsistente: esto no debería ocurrir porque acabamos de crear la solicitud.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resolucion.Error.Code);
            return Result.Fallo(resolucion.Error);
        }

        if (esNueva)
        {
            repositorio.Agregar(verificacion);
        }

        RegistrarEnvio(logger, request.UsuarioId, facial.Coinciden ? "pendiente-revision" : "rechazado-automatico");
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
        RuleFor(c => c.NumeroCi)
            .NotEmpty()
            .Matches("^[0-9]{5,12}$")
            .WithMessage("El número de CI no es válido.");

        RuleFor(c => c.ComplementoCi)
            .Matches("^[A-Za-z0-9]{1,5}$")
            .When(c => !string.IsNullOrWhiteSpace(c.ComplementoCi))
            .WithMessage("El complemento de CI no es válido.");

        RuleFor(c => c.DepartamentoExpedicion).IsInEnum();

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
