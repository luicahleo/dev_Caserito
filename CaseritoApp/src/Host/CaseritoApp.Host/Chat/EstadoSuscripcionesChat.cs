using Microsoft.AspNetCore.SignalR;

namespace CaseritoApp.Host.Chat;

public sealed class EstadoSuscripcionesChat(TimeProvider reloj)
{
    private const string Clave = "chat:suscripciones";

    public bool RegistrarInvocacion(HubCallerContext contexto, int maximoPorMinuto)
    {
        var estado = Obtener(contexto);
        var umbral = reloj.GetUtcNow().Subtract(TimeSpan.FromMinutes(1));
        lock (estado)
        {
            while (estado.Invocaciones.TryPeek(out var primera) && primera < umbral)
            {
                estado.Invocaciones.Dequeue();
            }

            if (estado.Invocaciones.Count >= maximoPorMinuto)
            {
                return false;
            }

            estado.Invocaciones.Enqueue(reloj.GetUtcNow());
            return true;
        }
    }

    public static bool PuedeAgregar(HubCallerContext contexto, Guid conversacionId, int maximo)
    {
        var estado = Obtener(contexto);
        lock (estado)
        {
            return estado.Conversaciones.Contains(conversacionId)
                || estado.Conversaciones.Count < maximo;
        }
    }

    public static void Agregar(HubCallerContext contexto, Guid conversacionId)
    {
        var estado = Obtener(contexto);
        lock (estado)
        {
            estado.Conversaciones.Add(conversacionId);
        }
    }

    public static void Quitar(HubCallerContext contexto, Guid conversacionId)
    {
        var estado = Obtener(contexto);
        lock (estado)
        {
            estado.Conversaciones.Remove(conversacionId);
        }
    }

    private static EstadoConexion Obtener(HubCallerContext contexto)
    {
        lock (contexto.Items)
        {
            if (!contexto.Items.TryGetValue(Clave, out var valor))
            {
                valor = new EstadoConexion();
                contexto.Items[Clave] = valor;
            }

            return (EstadoConexion)valor!;
        }
    }

    private sealed class EstadoConexion
    {
        public HashSet<Guid> Conversaciones { get; } = [];

        public Queue<DateTimeOffset> Invocaciones { get; } = [];
    }
}
