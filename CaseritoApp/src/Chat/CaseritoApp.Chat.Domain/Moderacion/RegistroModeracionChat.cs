using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Moderacion;

public sealed class RegistroModeracionChat : Entity
{
    private RegistroModeracionChat()
    {
    }

    private RegistroModeracionChat(
        Guid reporteId,
        Guid conversacionId,
        Guid moderadorId,
        AccionModeracionChat accion,
        DateTimeOffset creadoEn)
    {
        ReporteId = reporteId;
        ConversacionId = conversacionId;
        ModeradorId = moderadorId;
        Accion = accion;
        CreadoEn = creadoEn;
    }

    public Guid ReporteId { get; private set; }

    public Guid ConversacionId { get; private set; }

    public Guid ModeradorId { get; private set; }

    public AccionModeracionChat Accion { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Result<RegistroModeracionChat> Crear(
        Guid reporteId,
        Guid conversacionId,
        Guid moderadorId,
        AccionModeracionChat accion,
        DateTimeOffset creadoEn)
    {
        if (reporteId == Guid.Empty
            || conversacionId == Guid.Empty
            || moderadorId == Guid.Empty
            || !Enum.IsDefined(accion))
        {
            return Result.Fallo<RegistroModeracionChat>(new Error(
                ErroresModeracionChat.SolicitudInvalida,
                "La acción de moderación no es válida."));
        }

        return Result.Exito(new RegistroModeracionChat(
            reporteId,
            conversacionId,
            moderadorId,
            accion,
            creadoEn.ToUniversalTime()));
    }
}
