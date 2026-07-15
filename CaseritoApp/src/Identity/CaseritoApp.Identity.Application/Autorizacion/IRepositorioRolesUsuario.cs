using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>
/// Puerto para consultar y mutar los roles de los usuarios sin depender de ASP.NET Core Identity.
/// El adaptador (sobre <c>UserManager</c>) vive en Infrastructure. Las operaciones de mutación son
/// idempotentes: agregar un rol ya presente o quitar uno ausente no es error.
/// </summary>
public interface IRepositorioRolesUsuario
{
    /// <summary>Página de usuarios (filtrada por email/nombre si <paramref name="query"/> no es vacío) con sus roles.</summary>
    public Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(
        string? query, int pagina, int tamano, CancellationToken cancellationToken);

    /// <summary>Indica si existe un usuario con ese id.</summary>
    public Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Cantidad de usuarios que tienen el rol indicado.</summary>
    public Task<int> ContarEnRolAsync(string rol, CancellationToken cancellationToken);

    /// <summary>Indica si el usuario tiene el rol indicado.</summary>
    public Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken cancellationToken);

    /// <summary>Asigna el rol al usuario (idempotente si ya lo tiene).</summary>
    public Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken cancellationToken);

    /// <summary>Quita el rol al usuario (idempotente si no lo tiene).</summary>
    public Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken cancellationToken);
}
