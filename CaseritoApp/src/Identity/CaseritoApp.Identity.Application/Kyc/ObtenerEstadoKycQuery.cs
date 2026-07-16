using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Consulta el estado de verificación efectivo del usuario autenticado.</summary>
public sealed record ObtenerEstadoKycQuery(Guid UsuarioId) : IQuery<Result<EstadoKycDto>>;

/// <summary>Handler: proyecta el estado de la última solicitud, o "NoIniciado" si no hay ninguna.</summary>
public sealed class ObtenerEstadoKycQueryHandler(IRepositorioVerificacionKyc repositorio)
    : IQueryHandler<ObtenerEstadoKycQuery, Result<EstadoKycDto>>
{
    /// <summary>Estado expuesto cuando el usuario nunca envió una solicitud.</summary>
    public const string NoIniciado = "NoIniciado";

    public async Task<Result<EstadoKycDto>> Handle(ObtenerEstadoKycQuery request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorUsuarioAsync(request.UsuarioId, cancellationToken);
        var actual = verificacion?.SolicitudActual;

        var dto = actual is null
            ? new EstadoKycDto(NoIniciado, null)
            : new EstadoKycDto(actual.Estado.ToString(), actual.MotivoRechazo);

        return Result.Exito(dto);
    }
}
