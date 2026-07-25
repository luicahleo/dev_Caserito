namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record OrdenCreadaDto(
    Guid Id,
    Guid AvisoId,
    string Estado,
    decimal MontoAcordado,
    string Moneda,
    DateTimeOffset CreadaEn);

public sealed record OrdenResumenDto(
    Guid Id,
    Guid AvisoId,
    string Estado,
    decimal MontoAcordado,
    string Moneda,
    string Rol,
    DateTimeOffset ActualizadaEn);

public sealed record OrdenDetalleDto(
    Guid Id,
    Guid AvisoId,
    string Estado,
    decimal MontoAcordado,
    string Moneda,
    string Rol,
    DateTimeOffset CreadaEn,
    DateTimeOffset ActualizadaEn,
    DateTimeOffset? MarcadaVendidaEn,
    DateTimeOffset? CompradorConfirmoEn,
    DateTimeOffset? CompletadaEn);

public sealed record ResultadoPaginadoOrdenes(
    IReadOnlyList<OrdenResumenDto> Items,
    int Pagina,
    int Tamano,
    int Total);
