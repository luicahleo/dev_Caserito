using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Domain.PuntosEncuentro;

public sealed class PuntoEncuentroSeguro : AggregateRoot
{
    private PuntoEncuentroSeguro()
    {
        Nombre = null!;
        Ciudad = null!;
        Direccion = null!;
        Version = null!;
    }

    private PuntoEncuentroSeguro(
        Guid id,
        string nombre,
        string ciudad,
        string direccion,
        bool activo)
    {
        Id = id;
        Nombre = nombre;
        Ciudad = ciudad;
        Direccion = direccion;
        Activo = activo;
        Version = [];
    }

    public string Nombre { get; private set; }

    public string Ciudad { get; private set; }

    public string Direccion { get; private set; }

    public bool Activo { get; private set; }

    public byte[] Version { get; private set; }

    public static Result<PuntoEncuentroSeguro> Crear(
        string nombre,
        string ciudad,
        string direccion,
        bool activo = true,
        Guid? id = null)
    {
        var nombreNormalizado = nombre?.Trim();
        var ciudadNormalizada = ciudad?.Trim();
        var direccionNormalizada = direccion?.Trim();

        if (string.IsNullOrWhiteSpace(nombreNormalizado)
            || nombreNormalizado.Length > 150
            || string.IsNullOrWhiteSpace(ciudadNormalizada)
            || ciudadNormalizada.Length > 100
            || string.IsNullOrWhiteSpace(direccionNormalizada)
            || direccionNormalizada.Length > 250)
        {
            return Result.Fallo<PuntoEncuentroSeguro>(new Error(
                "punto_encuentro_invalido",
                "El punto de encuentro seguro no es válido."));
        }

        return Result.Exito(new PuntoEncuentroSeguro(
            id ?? Guid.NewGuid(),
            nombreNormalizado,
            ciudadNormalizada.ToLowerInvariant(),
            direccionNormalizada,
            activo));
    }
}
