namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Puerto para invalidar todas las sesiones renovables de un usuario.</summary>
public interface IRevocadorSesionesUsuario
{
    public Task RevocarTodasAsync(Guid usuarioId, CancellationToken cancellationToken);
}
