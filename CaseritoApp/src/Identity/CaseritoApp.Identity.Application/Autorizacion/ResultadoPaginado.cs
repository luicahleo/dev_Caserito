namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Página de resultados con metadatos de paginación.</summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total);
