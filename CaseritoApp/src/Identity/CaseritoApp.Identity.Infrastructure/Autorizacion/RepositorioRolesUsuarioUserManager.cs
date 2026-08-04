using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Autorizacion;

/// <summary>
/// Implementación de <see cref="IRepositorioRolesUsuario"/> sobre <see cref="UserManager{TUser}"/>.
/// La búsqueda proyecta roles por usuario con <c>GetRolesAsync</c> (N+1 acotado por el tamaño de
/// página; optimizable con join a AspNetUserRoles más adelante).
/// </summary>
public sealed class RepositorioRolesUsuarioUserManager(UserManager<ApplicationUser> userManager)
    : IRepositorioRolesUsuario
{
    public async Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(
        string? query, int pagina, int tamano, CancellationToken cancellationToken)
    {
        var consulta = userManager.Users;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var patron = query.Trim();
            consulta = consulta.Where(u =>
                (u.Email != null && u.Email.Contains(patron)) ||
                u.Nombres.Contains(patron) || u.Apellidos.Contains(patron));
        }

        var total = await consulta.CountAsync(cancellationToken);

        var usuarios = await consulta
            .OrderBy(u => u.Email)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        var items = new List<UsuarioConRolesDto>(usuarios.Count);
        foreach (var usuario in usuarios)
        {
            var roles = await userManager.GetRolesAsync(usuario);
            items.Add(new UsuarioConRolesDto(
                usuario.Id, usuario.Email ?? string.Empty,
                $"{usuario.Nombres} {usuario.Apellidos}".Trim(),
                usuario.CiudadId.ToString(), roles.ToArray()));
        }

        return new ResultadoPaginado<UsuarioConRolesDto>(items, pagina, tamano, total);
    }

    public async Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken cancellationToken) =>
        await userManager.FindByIdAsync(userId.ToString()) is not null;

    public async Task<int> ContarEnRolAsync(string rol, CancellationToken cancellationToken) =>
        (await userManager.GetUsersInRoleAsync(rol)).Count;

    public async Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        return usuario is not null && await userManager.IsInRoleAsync(usuario, rol);
    }

    public async Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        if (await userManager.IsInRoleAsync(usuario, rol))
        {
            return Result.Exito();
        }

        var resultado = await userManager.AddToRoleAsync(usuario, rol);
        return resultado.Succeeded
            ? Result.Exito()
            : Result.Fallo(new Error(
                "Roles.AsignacionFallida", string.Join("; ", resultado.Errors.Select(e => e.Description))));
    }

    public async Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        if (!await userManager.IsInRoleAsync(usuario, rol))
        {
            return Result.Exito();
        }

        var resultado = await userManager.RemoveFromRoleAsync(usuario, rol);
        return resultado.Succeeded
            ? Result.Exito()
            : Result.Fallo(new Error(
                "Roles.RetiroFallido", string.Join("; ", resultado.Errors.Select(e => e.Description))));
    }
}
