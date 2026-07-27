using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Domain.Notificaciones;

public sealed class Notificacion : AggregateRoot
{
    private Notificacion()
    {
        Titulo = null!;
        Mensaje = null!;
        Version = null!;
    }

    private Notificacion(
        Guid destinatarioId,
        TipoNotificacion tipo,
        string titulo,
        string mensaje,
        Guid? entidadRelacionadaId,
        DateTimeOffset creadaEn)
    {
        Id = Guid.NewGuid();
        DestinatarioId = destinatarioId;
        Tipo = tipo;
        Titulo = titulo;
        Mensaje = mensaje;
        EntidadRelacionadaId = entidadRelacionadaId;
        Leida = false;
        CreadaEn = creadaEn;
        Version = [];
    }

    public Guid DestinatarioId { get; private set; }
    public TipoNotificacion Tipo { get; private set; }
    public string Titulo { get; private set; }
    public string Mensaje { get; private set; }
    public Guid? EntidadRelacionadaId { get; private set; }
    public bool Leida { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public byte[] Version { get; private set; }

    public static Result<Notificacion> Crear(
        Guid destinatarioId,
        TipoNotificacion tipo,
        string titulo,
        string mensaje,
        Guid? entidadRelacionadaId,
        DateTimeOffset creadaEn)
    {
        var tituloNormalizado = titulo?.Trim();
        var mensajeNormalizado = mensaje?.Trim();

        if (destinatarioId == Guid.Empty
            || string.IsNullOrWhiteSpace(tituloNormalizado)
            || tituloNormalizado.Length > 150
            || string.IsNullOrWhiteSpace(mensajeNormalizado)
            || mensajeNormalizado.Length > 500)
        {
            return Result.Fallo<Notificacion>(new Error(
                ErroresNotificacion.Invalida,
                "La notificación no es válida."));
        }

        return Result.Exito(new Notificacion(
            destinatarioId,
            tipo,
            tituloNormalizado,
            mensajeNormalizado,
            entidadRelacionadaId,
            creadaEn.ToUniversalTime()));
    }

    public void MarcarComoLeida()
    {
        Leida = true;
    }
}
