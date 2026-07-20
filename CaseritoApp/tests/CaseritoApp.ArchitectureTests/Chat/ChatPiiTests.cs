using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.ArchitectureTests.Chat;

public sealed class ChatPiiTests
{
    [Fact]
    public void Contrato_de_mensaje_nuevo_no_expone_texto()
    {
        Assert.IsAssignableFrom<IIntegrationEvent>(new ChatMessageSent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        var propiedades = typeof(ChatMessageSent).GetProperties();

        Assert.DoesNotContain(propiedades, propiedad =>
            propiedad.PropertyType == typeof(string) ||
            propiedad.Name.Contains("Texto", StringComparison.OrdinalIgnoreCase) ||
            propiedad.Name.Contains("Contenido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Eventos_de_dominio_de_chat_no_transportan_texto()
    {
        var tiposEvento = typeof(Conversacion).Assembly.GetTypes()
            .Where(tipo => tipo.Namespace == typeof(Conversacion).Namespace)
            .Where(tipo => tipo.Name is nameof(ConversacionIniciada)
                or nameof(MensajeEnviado)
                or nameof(LecturaAvanzada));

        Assert.NotEmpty(tiposEvento);
        Assert.All(tiposEvento, tipo => Assert.DoesNotContain(
            tipo.GetProperties(),
            propiedad => propiedad.PropertyType == typeof(string)));
    }
}
