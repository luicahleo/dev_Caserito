using CaseritoApp.BuildingBlocks.Contracts.Catalog;
using CaseritoApp.Notifications.Application.Busquedas;
using CaseritoApp.Notifications.Domain.Notificaciones;
using MediatR;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed class NotificarProductoPublicadoHandler(
    ISender sender,
    IBusquedaGuardadaRepository busquedas,
    IConsultaProductoParaAlerta consulta,
    IEmailSender emailSender,
    IConsultaEmailUsuario consultaEmail)
    : INotificationHandler<ProductPublished>
{
    public async Task Handle(ProductPublished evento, CancellationToken cancellationToken)
    {
        var producto = await consulta.ObtenerAsync(evento.ProductId, cancellationToken);
        if (producto is null)
        {
            return;
        }

        var coincidentes = await busquedas.ListarCoincidenciasAsync(
            producto.Titulo,
            producto.Categoria,
            producto.Ciudad,
            producto.Precio,
            producto.EstadoProducto,
            cancellationToken);

        foreach (var busqueda in coincidentes)
        {
            _ = busqueda.Id;

            await sender.Send(
                new CrearNotificacionCommand(
                    busqueda.UsuarioId,
                    TipoNotificacion.AlertaBusqueda,
                    "Nuevo aviso que puede interesarte",
                    "Se publicó un aviso que coincide con tu búsqueda.",
                    evento.ProductId),
                cancellationToken);

            await EnviarEmailAsync(
                busqueda.UsuarioId,
                "Nuevo aviso que puede interesarte",
                "Se publicó un aviso que coincide con una de tus búsquedas guardadas.",
                cancellationToken);
        }
    }

    private async Task EnviarEmailAsync(
        Guid destinatarioId,
        string asunto,
        string cuerpo,
        CancellationToken ct)
    {
        try
        {
            var email = await consultaEmail.ObtenerEmailAsync(destinatarioId, ct);
            if (!string.IsNullOrWhiteSpace(email))
            {
                await emailSender.EnviarAsync(email, asunto, cuerpo, ct: ct);
            }
        }
        catch (Exception ex)
        {
            _ = ex;
            // Email no debe fallar la alerta in-app.
        }
    }
}
