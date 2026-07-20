using CaseritoApp.Chat.Infrastructure.TiempoReal;
using CaseritoApp.Host.Chat;
using Microsoft.AspNetCore.SignalR;

namespace CaseritoApp.ArchitectureTests.Chat;

public sealed class ChatTiempoRealArchitectureTests
{
    private static readonly string[] _camposMensaje =
        ["ConversacionId", "EnviadoEn", "Id", "RemitenteId", "Secuencia", "Texto"];

    [Fact]
    public void Outbox_no_contiene_payload_ni_identidades()
    {
        var nombres = typeof(EntregaTiempoReal).GetProperties().Select(x => x.Name).ToArray();

        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Texto", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Usuario", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Remitente", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Destinatario", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, nombre => nombre.Contains("Idempotencia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contrato_signalr_conserva_los_seis_campos_aprobados()
    {
        var nombres = typeof(MensajeTiempoRealDto).GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(_camposMensaje, nombres);
    }

    [Fact]
    public void Domain_y_application_no_referencian_signalr()
    {
        Assert.DoesNotContain(
            typeof(CaseritoApp.Chat.Domain.Conversaciones.Conversacion).Assembly.GetReferencedAssemblies(),
            x => x.Name!.Contains("SignalR", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(CaseritoApp.Chat.Application.Conversaciones.PuedeAccederConversacionQuery).Assembly
                .GetReferencedAssemblies(),
            x => x.Name!.Contains("SignalR", StringComparison.Ordinal));
    }

    [Fact]
    public void Hub_solo_expone_suscripcion_y_desuscripcion()
    {
        var metodos = typeof(ChatHub).GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(["DesuscribirConversacion", "SuscribirConversacion"], metodos);
        Assert.True(typeof(Hub).IsAssignableFrom(typeof(ChatHub)));
    }

    [Fact]
    public void Componentes_tiempo_real_no_inyectan_logging_con_datos()
    {
        var tipos = new[]
        {
            typeof(ChatHub),
            typeof(PublicadorSignalRMensajes),
            typeof(DespachadorEntregasTiempoReal),
        };

        Assert.All(tipos, tipo => Assert.DoesNotContain(
            tipo.GetConstructors().SelectMany(x => x.GetParameters()),
            parametro => parametro.ParameterType.FullName?.Contains("ILogger", StringComparison.Ordinal) == true));
    }
}
