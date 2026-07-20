namespace CaseritoApp.Chat.Application.Paginacion;

public sealed record PaginaCursor<TItem, TCursor>(
    IReadOnlyList<TItem> Items,
    TCursor? Siguiente)
    where TCursor : struct;
