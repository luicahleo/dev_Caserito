using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Chat;

namespace CaseritoApp.ArchitectureTests.Chat;

public sealed class ChatPiiTests
{
    [Fact]
    public void DtosDeColaYRegistrosDeAuditoria_NoExponenCamposProhibidos()
    {
        string[] nombresProhibidos =
        [
            "Texto",
            "Contenido",
            "Detalle",
            "Participante",
            "Reportante",
            "Comprador",
            "Vendedor",
            "Autor",
            "Payload",
            "Argumento"
        ];

        Type[] tiposProtegidos =
        [
            typeof(ReporteChatColaDto),
            typeof(RegistroModeracionChat)
        ];

        Assert.All(tiposProtegidos, tipo =>
            Assert.DoesNotContain(tipo.GetProperties(), propiedad =>
                nombresProhibidos.Any(nombre =>
                    propiedad.Name.Contains(nombre, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void PlantillasDeLogDeChat_NoIncluyenDatosSensibles()
    {
        string[] fragmentosProhibidos =
        [
            "Mensaje",
            "Reporte",
            "Payload",
            "Token",
            "Id",
            "Grupo",
            "Idempotencia",
            "Argumento",
            "Participante",
            "Connection"
        ];

        var plantillas = new[]
            {
                typeof(Conversacion).Assembly,
                typeof(ReporteChatColaDto).Assembly,
                typeof(ChatDbContext).Assembly,
                typeof(ChatHub).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(tipo => tipo.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Instance))
            .SelectMany(metodo => metodo.CustomAttributes)
            .Where(atributo =>
                atributo.AttributeType.FullName == "Microsoft.Extensions.Logging.LoggerMessageAttribute")
            .SelectMany(atributo => atributo.NamedArguments)
            .Where(argumento => argumento.MemberName == "Message")
            .Select(argumento => argumento.TypedValue.Value as string)
            .OfType<string>();

        Assert.DoesNotContain(plantillas, plantilla =>
            fragmentosProhibidos.Any(fragmento =>
                plantilla.Contains(fragmento, StringComparison.OrdinalIgnoreCase)));
    }

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
