namespace CaseritoApp.Notifications.Application.Notificaciones;

/// <summary>
/// Puerto de aplicación para obtener los datos mínimos de un aviso/producto
/// necesarios para generar una alerta de búsqueda.
/// </summary>
public interface IConsultaProductoParaAlerta
{
    public Task<ProductoAlertaDto?> ObtenerAsync(Guid avisoId, CancellationToken ct);
}

public sealed record ProductoAlertaDto(
    Guid AvisoId,
    string Titulo,
    string? Categoria,
    string? Ciudad,
    decimal? Precio,
    string? EstadoProducto);
