namespace CaseritoApp.Orders.Domain.Ordenes;

public enum EstadoOrden
{
    Requested = 1,
    Agreed = 2,
    Cancelled = 3,
    MarkedAsSold = 4,
    Completed = 5,
}
