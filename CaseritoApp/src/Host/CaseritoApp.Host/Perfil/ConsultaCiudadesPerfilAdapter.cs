using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Identity.Application.Perfil;

namespace CaseritoApp.Host.Perfil;

/// <summary>Adapta el catálogo canónico de ciudades al puerto requerido por Identity.</summary>
public sealed class ConsultaCiudadesPerfilAdapter(IConsultaCatalogo catalogo)
    : IConsultaCiudadesPerfil
{
    public Task<bool> ExisteActivaAsync(Guid ciudadId, CancellationToken ct) =>
        catalogo.ExisteCiudadActivaAsync(ciudadId, ct);

    public async Task<string?> ObtenerNombreActivaAsync(Guid ciudadId, CancellationToken ct) =>
        (await catalogo.ListarCiudadesAsync(ct)).FirstOrDefault(c => c.Id == ciudadId)?.Nombre;
}
