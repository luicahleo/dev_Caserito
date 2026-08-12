using System.IdentityModel.Tokens.Jwt;
using CaseritoApp.Chat.Application.Conversaciones;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Host.Chat;

[Authorize]
public sealed class ChatHub(
    ISender sender,
    EstadoSuscripcionesChat estado,
    RegistroConexionesChat registro,
    IOptions<OpcionesTiempoRealChat> opciones) : Hub
{
    public const string Politica = "chat-hub";
    private const string ErrorGenerico = "No fue posible completar la operación.";
    private readonly OpcionesTiempoRealChat _opciones = opciones.Value;

    public override async Task OnConnectedAsync()
    {
        var usuarioId = ObtenerUsuarioId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GruposChat.ParaUsuario(usuarioId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task SuscribirConversacion(Guid conversacionId)
    {
        var usuarioId = ObtenerUsuarioId();
        ValidarInvocacion(conversacionId);
        if (!EstadoSuscripcionesChat.PuedeAgregar(
                Context, conversacionId, _opciones.MaximoConversaciones))
        {
            throw new HubException(ErrorGenerico);
        }

        var autorizado = false;
        await registro.EjecutarExclusivoAsync(
            conversacionId,
            async () =>
            {
                autorizado = await sender.Send(
                    new PuedeRecibirTiempoRealQuery(conversacionId, usuarioId),
                    Context.ConnectionAborted);
                if (!autorizado)
                {
                    return;
                }

                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    GruposChat.ParaConversacion(conversacionId),
                    Context.ConnectionAborted);
                EstadoSuscripcionesChat.Agregar(Context, conversacionId);
                registro.Registrar(usuarioId, conversacionId, Context.ConnectionId);
            },
            Context.ConnectionAborted);
        if (!autorizado)
        {
            throw new HubException(ErrorGenerico);
        }
    }

    public async Task DesuscribirConversacion(Guid conversacionId)
    {
        _ = ObtenerUsuarioId();
        ValidarInvocacion(conversacionId);
        await registro.EjecutarExclusivoAsync(
            conversacionId,
            async () =>
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    GruposChat.ParaConversacion(conversacionId),
                    Context.ConnectionAborted);
                EstadoSuscripcionesChat.Quitar(Context, conversacionId);
                registro.Quitar(conversacionId, Context.ConnectionId);
            },
            Context.ConnectionAborted);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registro.QuitarConexion(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private Guid ObtenerUsuarioId()
    {
        var valor = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(valor, out var usuarioId) || usuarioId == Guid.Empty)
        {
            throw new HubException(ErrorGenerico);
        }

        return usuarioId;
    }

    private void ValidarInvocacion(Guid conversacionId)
    {
        if (conversacionId == Guid.Empty
            || !estado.RegistrarInvocacion(Context, _opciones.MaximoInvocacionesPorMinuto))
        {
            throw new HubException(ErrorGenerico);
        }
    }
}
