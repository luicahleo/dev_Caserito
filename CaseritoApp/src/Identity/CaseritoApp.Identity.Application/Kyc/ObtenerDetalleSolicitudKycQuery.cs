using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

public sealed record DetalleSolicitudKycDto(
    Guid SolicitudId,
    string Nombres,
    string Apellidos,
    string NumeroCi,
    string? ComplementoCi,
    string DepartamentoExpedicion,
    string Estado,
    double? ScoreSimilitud);

public sealed record ObtenerDetalleSolicitudKycQuery(Guid SolicitudId, Guid AdminId)
    : IQuery<Result<DetalleSolicitudKycDto>>;

public sealed class ObtenerDetalleSolicitudKycQueryHandler(
    IRepositorioVerificacionKyc repositorio,
    IAuditorAccesoPii auditor)
    : IQueryHandler<ObtenerDetalleSolicitudKycQuery, Result<DetalleSolicitudKycDto>>
{
    public async Task<Result<DetalleSolicitudKycDto>> Handle(
        ObtenerDetalleSolicitudKycQuery request,
        CancellationToken cancellationToken)
    {
        var detalle = await repositorio.ObtenerDetalleAsync(request.SolicitudId, cancellationToken);
        if (detalle is null)
        {
            return Result.Fallo<DetalleSolicitudKycDto>(
                new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        await auditor.RegistrarAccesoAsync(
            $"kyc:{request.SolicitudId}:detalle-documental",
            request.AdminId.ToString(),
            cancellationToken);
        return Result.Exito(detalle);
    }
}
