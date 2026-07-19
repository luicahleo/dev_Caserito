using CaseritoApp.Catalog.Domain.Moderacion;

namespace CaseritoApp.Catalog.Application.Moderacion;

public interface IRepositorioRegistrosModeracion
{
    public void Agregar(RegistroModeracion registro);
}
