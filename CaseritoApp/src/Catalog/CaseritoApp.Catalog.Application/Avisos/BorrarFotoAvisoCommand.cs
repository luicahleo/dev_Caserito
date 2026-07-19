using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Borra una foto del aviso. Solo el due&#xf1;o puede borrar.</summary>
public sealed record BorrarFotoAvisoCommand(
    Guid AvisoId,
    Guid FotoId,
    Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="BorrarFotoAvisoCommand"/>.</summary>
public sealed partial class BorrarFotoAvisoCommandHandler(
    IRepositorioAvisos repositorio,
    IAlmacenFotosAviso almacen,
    ILogger<BorrarFotoAvisoCommandHandler> logger)
    : ICommandHandler<BorrarFotoAvisoCommand>
{
    public async Task<Result> Handle(BorrarFotoAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerConFotosAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        var resultado = aviso.QuitarFoto(request.FotoId);
        if (!resultado.EsExito)
        {
            return Result.Fallo(resultado.Error);
        }

        // Persistir la eliminaci&#xf3;n de la entidad (UnitOfWorkBehavior llama SaveChanges despu&#xe9;s).
        // El blob se borra en best-effort: si falla, queda hu&#xe9;rfano (aceptable en MVP).
        var clave = resultado.Valor.Clave;
        await BorrarBlobBestEffortAsync(clave, request.AvisoId, request.FotoId, cancellationToken);

        return Result.Exito();
    }

    private async Task BorrarBlobBestEffortAsync(
        string clave, Guid avisoId, Guid fotoId, CancellationToken ct)
    {
        try
        {
            await almacen.EliminarAsync(clave, ct);
        }
        catch (Exception ex)
        {
            LogWarningBorrarBlob(logger, ex, clave, fotoId, avisoId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo borrar el blob {Clave} de la foto {FotoId} del aviso {AvisoId}")]
    private static partial void LogWarningBorrarBlob(ILogger logger, Exception ex, string clave, Guid fotoId, Guid avisoId);
}
