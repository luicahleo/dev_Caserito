using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>Consulta el perfil del usuario autenticado identificado por <paramref name="UserId"/>.</summary>
public sealed record ObtenerPerfilQuery(Guid UserId) : IQuery<Result<PerfilDto>>;

/// <summary>Handler de <see cref="ObtenerPerfilQuery"/>: delega en <see cref="IRepositorioPerfil"/>.</summary>
public sealed class ObtenerPerfilQueryHandler(IRepositorioPerfil repositorioPerfil)
    : IQueryHandler<ObtenerPerfilQuery, Result<PerfilDto>>
{
    public async Task<Result<PerfilDto>> Handle(ObtenerPerfilQuery request, CancellationToken cancellationToken)
    {
        var perfil = await repositorioPerfil.ObtenerAsync(request.UserId, cancellationToken);

        return perfil is null
            ? Result.Fallo<PerfilDto>(new Error("Perfil.NoEncontrado", "El usuario no existe."))
            : Result.Exito(perfil);
    }
}
