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
    IOptions<OpcionesTiempoRealChat> opciones) : Hub
{
    public const string Politica = "chat-hub";
    private const string ErrorGenerico = "No fue posible completar la operación.";
    private readonly OpcionesTiempoRealChat _opciones = opciones.Value;

    public async Task SuscribirConversacion(Guid conversacionId)
    {
        var usuarioId = ObtenerUsuarioId();
        ValidarInvocacion(conversacionId);
        if (!EstadoSuscripcionesChat.PuedeAgregar(
                Context, conversacionId, _opciones.MaximoConversaciones))
        {
            throw new HubException(ErrorGenerico);
        }

        var autorizado = await sender.Send(
            new PuedeAccederConversacionQuery(conversacionId, usuarioId),
            Context.ConnectionAborted);
        if (!autorizado)
        {
            throw new HubException(ErrorGenerico);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GruposChat.ParaConversacion(conversacionId),
            Context.ConnectionAborted);
        EstadoSuscripcionesChat.Agregar(Context, conversacionId);
    }

    public async Task DesuscribirConversacion(Guid conversacionId)
    {
        _ = ObtenerUsuarioId();
        ValidarInvocacion(conversacionId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GruposChat.ParaConversacion(conversacionId),
            Context.ConnectionAborted);
        EstadoSuscripcionesChat.Quitar(Context, conversacionId);
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
