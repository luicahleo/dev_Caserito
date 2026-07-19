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
                "La imagen no es v&#xe1;lida. Se aceptan jpeg/png de hasta 5 MiB."));
        }

        // Guardar blob antes de persistir la entidad.
        string clave;
        try
        {
            clave = await almacen.GuardarAsync(request.Contenido, request.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorGuardarBlob(logger, ex, request.AvisoId);
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.ErrorAlmacenamiento,
                "No se pudo almacenar la imagen. Int&#xe9;ntalo de nuevo."));
        }

        var resultado = aviso.AgregarFoto(clave, request.ContentType);
        if (!resultado.EsExito)
        {
            // La colecci&#xf3;n est&#xe1; llena: borrar el blob reci&#xe9;n guardado (best-effort).
            await BorrarBlobBestEffortAsync(clave, request.AvisoId, cancellationToken);
            return Result.Fallo<Guid>(resultado.Error);
        }

        // La foto se persistir&#xe1; al hacer SaveChanges (UnitOfWorkBehavior).
        return Result.Exito(aviso.Fotos[^1].Id);
    }

    private async Task BorrarBlobBestEffortAsync(string clave, Guid avisoId, CancellationToken ct)
    {
        try
        {
            await almacen.EliminarAsync(clave, ct);
        }
        catch (Exception ex)
        {
            LogWarningBlobHuerfano(logger, ex, clave, avisoId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error al guardar el blob de foto para aviso {AvisoId}")]
    private static partial void LogErrorGuardarBlob(ILogger logger, Exception ex, Guid avisoId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo borrar el blob huérfano {Clave} del aviso {AvisoId}")]
    private static partial void LogWarningBlobHuerfano(ILogger logger, Exception ex, string clave, Guid avisoId);
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
