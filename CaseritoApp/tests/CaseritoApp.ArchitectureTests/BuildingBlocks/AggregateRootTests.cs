using CaseritoApp.BuildingBlocks.Domain;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class AggregateRootTests
{
    private sealed record EventoPrueba : IDomainEvent;

    private sealed class AgregadoPrueba : AggregateRoot
    {
        public void Hacer() => AgregarEvento(new EventoPrueba());
    }

    [Fact]
    public void Agrega_y_limpia_eventos_de_dominio()
    {
        var agregado = new AgregadoPrueba();
        agregado.Hacer();
        Assert.Single(agregado.EventosDeDominio);

        agregado.LimpiarEventos();
        Assert.Empty(agregado.EventosDeDominio);
    }
}
