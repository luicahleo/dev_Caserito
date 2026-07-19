using CaseritoApp.Catalog.Application.Moderacion;
using CaseritoApp.Catalog.Domain.Moderacion;

namespace CaseritoApp.Catalog.Infrastructure.Moderacion;

public sealed class RepositorioRegistrosModeracionEfCore(CatalogDbContext db) : IRepositorioRegistrosModeracion
{
    public void Agregar(RegistroModeracion registro) => db.RegistrosModeracion.Add(registro);
}
