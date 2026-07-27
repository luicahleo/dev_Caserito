namespace CaseritoApp.Notifications.Application.Busquedas;

public sealed record BusquedaGuardadaDto(
    Guid Id,
    string? PalabraClave,
    string? Categoria,
    string? Ciudad,
    decimal? PrecioMinimo,
    decimal? PrecioMaximo,
    string? EstadoProducto,
    DateTimeOffset CreadaEn);
