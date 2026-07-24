namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record OrdenCreadaDto(
    Guid Id,
    Guid AvisoId,
    string Estado,
    decimal MontoAcordado,
    string Moneda,
    DateTimeOffset CreadaEn);
