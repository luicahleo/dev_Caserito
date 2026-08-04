using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>Datos mínimos que Identity permite publicar de un usuario.</summary>
public sealed record PerfilPublicoDto(
    Guid Id,
    string NombreVisible,
    Guid CiudadId,
    string NombreCiudad,
    bool Verificado);

/// <summary>Consulta anónima del perfil público mínimo.</summary>
public sealed record ObtenerPerfilPublicoQuery(Guid UserId)
    : IQuery<Result<PerfilPublicoDto>>;

public sealed class ObtenerPerfilPublicoQueryHandler(IRepositorioPerfil repositorioPerfil)
    : IQueryHandler<ObtenerPerfilPublicoQuery, Result<PerfilPublicoDto>>
{
    public async Task<Result<PerfilPublicoDto>> Handle(
        ObtenerPerfilPublicoQuery request,
        CancellationToken cancellationToken)
    {
        var perfil = await repositorioPerfil.ObtenerPublicoAsync(
            request.UserId,
            cancellationToken);

        return perfil is null
            ? Result.Fallo<PerfilPublicoDto>(new Error(
                "reputacion_usuario_no_disponible",
                "El recurso no está disponible."))
            : Result.Exito(perfil);
    }
}
