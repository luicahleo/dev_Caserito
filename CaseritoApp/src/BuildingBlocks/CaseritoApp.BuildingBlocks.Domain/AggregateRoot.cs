namespace CaseritoApp.BuildingBlocks.Domain;

/// <summary>
/// Clase base para las raíces de agregado, que acumulan eventos de dominio hasta su publicación.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _eventos = [];

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Eventos de dominio pendientes de publicar.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> EventosDeDominio => _eventos.AsReadOnly();

    /// <summary>
    /// Agrega un evento de dominio a la colección pendiente de publicación.
    /// </summary>
    protected void AgregarEvento(IDomainEvent evento) => _eventos.Add(evento);

    /// <summary>
    /// Limpia los eventos de dominio pendientes, típicamente tras publicarlos.
    /// </summary>
    public void LimpiarEventos() => _eventos.Clear();
}
