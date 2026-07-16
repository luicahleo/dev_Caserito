using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Cuál de los dos blobs de una solicitud se solicita.</summary>
public enum TipoBlobKyc
{
    Documento,
    Selfie,
}

/// <summary>Obtiene (descifrado) el documento o la selfie de una solicitud. Registra el acceso a PII.</summary>
public sealed record ObtenerBlobKycQuery(Guid SolicitudId, Guid AdminId, TipoBlobKyc Tipo)
    : IQuery<Result<BlobKyc>>;

/// <summary>Handler: localiza la solicitud, audita el acceso y devuelve el blob descifrado.</summary>
public sealed class ObtenerBlobKycQueryHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    IAuditorAccesoPii auditor)
    : IQueryHandler<ObtenerBlobKycQuery, Result<BlobKyc>>
{
    public async Task<Result<BlobKyc>> Handle(ObtenerBlobKycQuery request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        var solicitud = verificacion?.Solicitudes.FirstOrDefault(s => s.Id == request.SolicitudId);
        if (solicitud is null)
        {
            return Result.Fallo<BlobKyc>(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var clave = request.Tipo == TipoBlobKyc.Documento
            ? solicitud.ReferenciaDocumento
            : solicitud.ReferenciaSelfie;

        // Recurso auditado: id de solicitud + tipo; NUNCA la clave opaca del blob.
        await auditor.RegistrarAccesoAsync(
            $"kyc:{request.SolicitudId}:{request.Tipo}", request.AdminId.ToString(), cancellationToken);

        var blob = await almacen.ObtenerAsync(clave, cancellationToken);
        return Result.Exito(blob);
    }
}
