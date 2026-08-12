using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Sube una foto al aviso. Solo el due&#xf1;o puede subir. M&#xe1;x. 5 fotos por aviso.</summary>
public sealed record SubirFotoAvisoCommand(
    Guid AvisoId,
    Guid VendedorId,
    byte[] Contenido,
    string ContentType) : ICommand<Guid>;

/// <summary>Handler de <see cref="SubirFotoAvisoCommand"/>.</summary>
public sealed partial class SubirFotoAvisoCommandHandler(
    IRepositorioAvisos repositorio,
    IAlmacenFotosAviso almacen,
    IProcesadorFotoAviso procesador,
    ILogger<SubirFotoAvisoCommandHandler> logger)
    : ICommandHandler<SubirFotoAvisoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubirFotoAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerConFotosAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        if (!ValidacionFotoAviso.EsImagenValida(request.Contenido, request.ContentType))
        {
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.ImagenInvalida,
                "La imagen no es válida. Se aceptan JPEG/PNG de hasta 25 MiB."));
        }

        FotoAvisoProcesada? fotoProcesada;
        try
        {
            fotoProcesada = await procesador.ProcesarAsync(
                request.Contenido,
                request.ContentType,
                cancellationToken);
        }
        catch (Exception)
        {
            LogErrorProcesarFoto(logger);
            fotoProcesada = null;
        }

        if (fotoProcesada is null)
        {
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.ImagenInvalida,
                "No se pudo procesar la imagen."));
        }

        // Guardar únicamente el resultado normalizado antes de persistir la entidad.
        string clave;
        try
        {
            clave = await almacen.GuardarAsync(
                fotoProcesada.Contenido,
                fotoProcesada.ContentType,
                cancellationToken);
        }
        catch (Exception)
        {
            LogErrorGuardarBlob(logger);
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.ErrorAlmacenamiento,
                "No se pudo almacenar la imagen. Int&#xe9;ntalo de nuevo."));
        }

        var resultado = aviso.AgregarFoto(clave, fotoProcesada.ContentType);
        if (!resultado.EsExito)
        {
            // La colecci&#xf3;n est&#xe1; llena: borrar el blob reci&#xe9;n guardado (best-effort).
            await BorrarBlobBestEffortAsync(clave, cancellationToken);
            return Result.Fallo<Guid>(resultado.Error);
        }

        // La foto se persistir&#xe1; al hacer SaveChanges (UnitOfWorkBehavior).
        return Result.Exito(aviso.Fotos[^1].Id);
    }

    private async Task BorrarBlobBestEffortAsync(string clave, CancellationToken ct)
    {
        try
        {
            await almacen.EliminarAsync(clave, ct);
        }
        catch (Exception)
        {
            LogWarningBlobHuerfano(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo procesar una foto de aviso")]
    private static partial void LogErrorProcesarFoto(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error al guardar una foto de aviso")]
    private static partial void LogErrorGuardarBlob(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo borrar una foto huérfana")]
    private static partial void LogWarningBlobHuerfano(ILogger logger);
}

/// <summary>Valida <see cref="SubirFotoAvisoCommand"/>.</summary>
public sealed class SubirFotoAvisoCommandValidator : AbstractValidator<SubirFotoAvisoCommand>
{
    public SubirFotoAvisoCommandValidator()
    {
        RuleFor(c => c.AvisoId).NotEmpty().WithMessage("El id del aviso es obligatorio.");
        RuleFor(c => c.VendedorId).NotEmpty().WithMessage("El id del vendedor es obligatorio.");
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El contenido de la imagen es obligatorio.");
        RuleFor(c => c.ContentType).NotEmpty().WithMessage("El content-type es obligatorio.");
    }
}
