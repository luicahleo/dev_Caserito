using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Ciudad de referencia del catálogo (sembrada, no gestionable por UI en el MVP).</summary>
public sealed class Ciudad : Entity
{
    // Constructor para EF Core.
#pragma warning disable S1144 // El constructor privado sin parámetros es necesario para EF Core.
    private Ciudad() => Nombre = null!;
#pragma warning restore S1144

    private Ciudad(Guid id, string nombre, int orden) : base(id)
    {
        Nombre = nombre;
        Orden = orden;
        Activa = true;
    }

    /// <summary>Nombre visible de la ciudad.</summary>
    public string Nombre { get; private set; }

    /// <summary>Indica si la ciudad está disponible para publicar/filtrar.</summary>
    public bool Activa { get; private set; }

    /// <summary>Orden de presentación.</summary>
    public int Orden { get; private set; }

    /// <summary>Crea una ciudad activa con id fijo (para el seeder idempotente).</summary>
    public static Ciudad Crear(Guid id, string nombre, int orden) => new(id, nombre, orden);
}
