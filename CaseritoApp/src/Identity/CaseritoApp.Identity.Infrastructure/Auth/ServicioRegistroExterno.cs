using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Usuarios;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Auth;

public enum EstadoRegistroExterno
{
    Completado,
    RequiereVinculacion,
    Fallo,
}

public sealed record ResultadoRegistroExterno(EstadoRegistroExterno Estado, ApplicationUser? Usuario);

/// <summary>Crea y asocia una cuenta externa sin dejar usuarios parciales.</summary>
public sealed class ServicioRegistroExterno(
    UserManager<ApplicationUser> usuarios,
    IPublisher publisher,
    TimeProvider tiempo)
{
    public async Task<bool> VincularAsync(LoginExternoPendiente pendiente, Guid usuarioId)
    {
        var asociado = await usuarios.FindByLoginAsync(pendiente.Proveedor, pendiente.ClaveProveedor);
        if (asociado is not null)
        {
            return asociado.Id == usuarioId;
        }

        if (string.IsNullOrWhiteSpace(pendiente.Email))
        {
            return false;
        }

        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario?.Email is null ||
            !string.Equals(
                usuarios.NormalizeEmail(usuario.Email),
                usuarios.NormalizeEmail(pendiente.Email),
                StringComparison.Ordinal))
        {
            return false;
        }

        var resultado = await usuarios.AddLoginAsync(
            usuario,
            new UserLoginInfo(pendiente.Proveedor, pendiente.ClaveProveedor, pendiente.Proveedor));
        if (resultado.Succeeded)
        {
            return true;
        }

        asociado = await usuarios.FindByLoginAsync(pendiente.Proveedor, pendiente.ClaveProveedor);
        return asociado?.Id == usuarioId;
    }

    public async Task<ResultadoRegistroExterno> RegistrarAsync(
        LoginExternoPendiente pendiente,
        string email,
        string nombres,
        string apellidos,
        Guid ciudadId,
        CancellationToken ct)
    {
        var asociado = await usuarios.FindByLoginAsync(pendiente.Proveedor, pendiente.ClaveProveedor);
        if (asociado is not null)
        {
            return new(EstadoRegistroExterno.Completado, asociado);
        }

        var emailNormalizado = usuarios.NormalizeEmail(email);
        var existente = await usuarios.FindByEmailAsync(emailNormalizado);
        if (existente is not null)
        {
            return new(EstadoRegistroExterno.RequiereVinculacion, null);
        }

        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = pendiente.Proveedor == "google" && pendiente.EmailConfiable &&
                string.Equals(pendiente.Email, email, StringComparison.OrdinalIgnoreCase),
            Nombres = nombres,
            Apellidos = apellidos,
            CiudadId = ciudadId,
        };
        if (!(await usuarios.CreateAsync(usuario)).Succeeded)
        {
            return await ResolverCarreraAsync(pendiente, email);
        }

        if (!(await usuarios.AddToRoleAsync(usuario, RolesApp.Cliente)).Succeeded ||
            !(await usuarios.AddLoginAsync(
                usuario,
                new UserLoginInfo(pendiente.Proveedor, pendiente.ClaveProveedor, pendiente.Proveedor))).Succeeded)
        {
            await usuarios.DeleteAsync(usuario);
            return await ResolverCarreraAsync(pendiente, email);
        }

        if (!usuario.EmailConfirmed)
        {
            await publisher.Publish(
                new UsuarioRegistrado(Guid.NewGuid(), tiempo.GetUtcNow(), usuario.Id, usuario.Email!, usuario.Nombres),
                ct);
        }

        return new(EstadoRegistroExterno.Completado, usuario);
    }

    private async Task<ResultadoRegistroExterno> ResolverCarreraAsync(
        LoginExternoPendiente pendiente,
        string email)
    {
        var asociado = await usuarios.FindByLoginAsync(pendiente.Proveedor, pendiente.ClaveProveedor);
        if (asociado is not null)
        {
            return new(EstadoRegistroExterno.Completado, asociado);
        }

        return await usuarios.FindByEmailAsync(email) is not null
            ? new(EstadoRegistroExterno.RequiereVinculacion, null)
            : new(EstadoRegistroExterno.Fallo, null);
    }
}
