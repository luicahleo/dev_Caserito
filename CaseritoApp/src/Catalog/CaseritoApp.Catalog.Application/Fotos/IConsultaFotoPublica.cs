namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>Verifica que una foto pertenezca a un aviso públicamente visible.</summary>
public interface IConsultaFotoPublica
{
    public Task<bool> EsPublicaAsync(string clave, CancellationToken ct);
}
