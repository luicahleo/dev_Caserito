using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Infrastructure.TiempoReal;
using CaseritoApp.Host.Chat;
using Microsoft.AspNetCore.SignalR;

namespace CaseritoApp.ArchitectureTests.Chat;

public sealed class ChatTiempoRealArchitectureTests
{
    private static readonly string[] _camposMensaje =
        ["ConversacionId", "EnviadoEn", "Id", "RemitenteId", "Secuencia", "Texto"];
    private static readonly string[] _camposEstado =
        ["ConversacionId", "UltimaSecuenciaEntregada", "UltimaSecuenciaLeida"];

    [Fact]
    public void EventosDeRevocacion_NoTransportanParticipantesNiContenido()
    {
        Type[] eventos =
        [
            typeof(AccesoTiempoRealRevocado),
            typeof(BloqueoTiempoRealConfirmado),
            typeof(AccesoTiempoRealRevocadoPorReporte)
        ];
        string[] camposProhibidos =
        [
            "Usuario",
            "Participante",
            "Comprador",
            "Vendedor",
            "Remitente",
            "Destinatario",
            "Texto",
            "Contenido",
            "Detalle",
            "Payload"
        ];

        Assert.All(eventos, tipo =>
            Assert.DoesNotContain(tipo.GetProperties(), propiedad =>
                camposProhibidos.Any(campo =>
                    propiedad.Name.Contains(campo, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void Despachador_ConsultaElegibilidadAntesDePublicarSignalR()
    {
        var metodo = typeof(DespachadorEntregasTiempoReal)
            .GetMethod(nameof(DespachadorEntregasTiempoReal.ProcesarLoteAsync))!;
        var maquinaEstados = metodo.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute),
                inherit: false)
            .Cast<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()
            .Single()
            .StateMachineType;
        var moverSiguiente = maquinaEstados.GetMethod(
            nameof(System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext),
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public)!;
        var referencias = ReferenciasDeMetodos(moverSiguiente).ToArray();

        var consulta = Array.FindIndex(referencias, referencia =>
            referencia.Metodo.DeclaringType == typeof(IConsultaConversaciones)
            && referencia.Metodo.Name == nameof(IConsultaConversaciones.PuedeRecibirTiempoRealAsync));
        var publicacion = Array.FindIndex(referencias, referencia =>
            referencia.Metodo.DeclaringType == typeof(IPublicadorMensajesTiempoReal)
            && referencia.Metodo.Name == nameof(IPublicadorMensajesTiempoReal.PublicarAsync));

        Assert.True(consulta >= 0, "El despachador debe consultar la elegibilidad.");
        Assert.True(publicacion > consulta, "La publicación debe ocurrir después de consultar la elegibilidad.");
    }

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
    public void Eventos_globales_son_minimos_y_no_transportan_contenido_ni_usuario()
    {
        var nombres = typeof(EstadoMensajesActualizadoDto).GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(_camposEstado, nombres);
        Assert.DoesNotContain(nombres, nombre =>
            nombre.Contains("Usuario", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Texto", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Contenido", StringComparison.OrdinalIgnoreCase));
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
            .Where(x => x.GetBaseDefinition().DeclaringType == typeof(ChatHub))
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

    private static IEnumerable<(int Desplazamiento, System.Reflection.MethodBase Metodo)> ReferenciasDeMetodos(
        System.Reflection.MethodInfo metodo)
    {
        var il = metodo.GetMethodBody()!.GetILAsByteArray()!;
        for (var desplazamiento = 0; desplazamiento <= il.Length - sizeof(int); desplazamiento++)
        {
            var token = BitConverter.ToInt32(il, desplazamiento);
            System.Reflection.MethodBase? referencia;
            try
            {
                referencia = metodo.Module.ResolveMethod(token);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (referencia is not null)
            {
                yield return (desplazamiento, referencia);
            }
        }
    }
}
