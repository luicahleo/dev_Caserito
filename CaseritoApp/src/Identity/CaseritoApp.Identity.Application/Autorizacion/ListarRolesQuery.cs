using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Domain.Autorizacion;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Consulta el catálogo de roles del MVP con los permisos que agrega cada uno.</summary>
public sealed record ListarRolesQuery : IQuery<IReadOnlyList<RolDto>>;

/// <summary>Handler de <see cref="ListarRolesQuery"/>: proyecta <c>RolesApp.Todos</c> sobre <c>MapaRolesPermisos</c>.</summary>
public sealed class ListarRolesQueryHandler : IQueryHandler<ListarRolesQuery, IReadOnlyList<RolDto>>
{
    public Task<IReadOnlyList<RolDto>> Handle(ListarRolesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RolDto> roles = RolesApp.Todos
            .Select(rol => new RolDto(rol, MapaRolesPermisos.PermisosDeRol(rol)))
            .ToArray();

        return Task.FromResult(roles);
    }
}
