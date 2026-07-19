namespace CaseritoApp.Catalog.Infrastructure.Fotos;

/// <summary>Opciones de configuración del almacén de fotos de avisos.</summary>
public sealed class OpcionesAlmacenFotos
{
    /// <summary>Ruta base donde se guardan los blobs. Configurable vía appsettings / env.</summary>
    public string RutaBase { get; set; } = "/data/fotos-avisos";
}
