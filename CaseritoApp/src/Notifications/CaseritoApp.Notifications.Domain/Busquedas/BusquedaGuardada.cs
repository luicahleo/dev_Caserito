using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Domain.Busquedas;

public sealed class BusquedaGuardada : AggregateRoot
{
    private BusquedaGuardada()
    {
        PalabraClave = null;
        Categoria = null;
        Ciudad = null;
        EstadoProducto = null;
        Version = null!;
    }

    private BusquedaGuardada(
        Guid usuarioId,
        string? palabraClave,
        string? categoria,
        string? ciudad,
        decimal? precioMinimo,
        decimal? precioMaximo,
        string? estadoProducto,
        DateTimeOffset creadaEn)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        PalabraClave = palabraClave;
        Categoria = categoria;
        Ciudad = ciudad;
        PrecioMinimo = precioMinimo;
        PrecioMaximo = precioMaximo;
        EstadoProducto = estadoProducto;
        CreadaEn = creadaEn;
        Version = [];
    }

    public Guid UsuarioId { get; private set; }
    public string? PalabraClave { get; private set; }
    public string? Categoria { get; private set; }
    public string? Ciudad { get; private set; }
    public decimal? PrecioMinimo { get; private set; }
    public decimal? PrecioMaximo { get; private set; }
    public string? EstadoProducto { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public byte[] Version { get; private set; }

    /// <summary>Valor especial para <see cref="EstadoProducto"/> que coincide con cualquier estado.</summary>
    public const string EstadoCualquiera = "cualquiera";

    public static Result<BusquedaGuardada> Crear(
        Guid usuarioId,
        string? palabraClave,
        string? categoria,
        string? ciudad,
        decimal? precioMinimo,
        decimal? precioMaximo,
        string? estadoProducto,
        DateTimeOffset creadaEn)
    {
        if (usuarioId == Guid.Empty
            || string.IsNullOrWhiteSpace(palabraClave)
                && string.IsNullOrWhiteSpace(categoria)
                && string.IsNullOrWhiteSpace(ciudad)
            || (precioMinimo.HasValue && precioMaximo.HasValue && precioMinimo > precioMaximo)
            || (precioMinimo.HasValue && precioMinimo < 0)
            || (precioMaximo.HasValue && precioMaximo < 0))
        {
            return Result.Fallo<BusquedaGuardada>(new Error(
                "busqueda_guardada_invalida",
                "La búsqueda guardada no es válida."));
        }

        return Result.Exito(new BusquedaGuardada(
            usuarioId,
            palabraClave?.Trim().ToLowerInvariant(),
            categoria?.Trim().ToLowerInvariant(),
            ciudad?.Trim().ToLowerInvariant(),
            precioMinimo,
            precioMaximo,
            estadoProducto?.Trim().ToLowerInvariant(),
            creadaEn.ToUniversalTime()));
    }
}
