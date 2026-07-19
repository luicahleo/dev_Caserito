namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>Foto de un aviso expuesta en los DTOs de catálogo.</summary>
public sealed record FotoAvisoDto(Guid Id, string Url, int Orden);
