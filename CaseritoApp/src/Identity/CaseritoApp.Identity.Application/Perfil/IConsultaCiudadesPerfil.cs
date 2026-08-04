namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>
/// Puerto de Identity para validar referencias de ciudad sin depender del contexto Catalog.
/// </summary>
public interface IConsultaCiudadesPerfil
{
    /// <summary>Indica si la ciudad referenciada existe y está activa.</summary>
    public Task<bool> ExisteActivaAsync(Guid ciudadId, CancellationToken ct);

    /// <summary>Obtiene el nombre canónico de una ciudad activa.</summary>
    public Task<string?> ObtenerNombreActivaAsync(Guid ciudadId, CancellationToken ct);
}
