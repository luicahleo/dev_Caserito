namespace CaseritoApp.BuildingBlocks.Domain;

/// <summary>
/// Clase base para las entidades de dominio, identificadas por un <see cref="Guid"/>.
/// </summary>
public abstract class Entity
{
    protected Entity() => Id = Guid.NewGuid();

    protected Entity(Guid id) => Id = id;

    /// <summary>
    /// Identificador único de la entidad.
    /// </summary>
    public Guid Id { get; protected init; }
}
