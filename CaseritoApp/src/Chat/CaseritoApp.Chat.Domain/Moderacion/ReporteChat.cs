using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Moderacion;

public sealed class ReporteChat : AggregateRoot
{
    private ReporteChat()
    {
    }

    private ReporteChat(
        Guid conversacionId,
        Guid reportanteId,
        TipoObjetivoReporteChat tipoObjetivo,
        Guid? mensajeId,
        CategoriaReporteChat categoria,
        string? detalle,
        DateTimeOffset creadoEn)
    {
        ConversacionId = conversacionId;
        ReportanteId = reportanteId;
        TipoObjetivo = tipoObjetivo;
        MensajeId = mensajeId;
        Categoria = categoria;
        Detalle = detalle;
        Estado = EstadoReporteChat.Pendiente;
        CreadoEn = creadoEn;
    }

    public Guid ConversacionId { get; private set; }

    public Guid ReportanteId { get; private set; }

    public TipoObjetivoReporteChat TipoObjetivo { get; private set; }

    public Guid? MensajeId { get; private set; }

    public CategoriaReporteChat Categoria { get; private set; }

    public string? Detalle { get; private set; }

    public EstadoReporteChat Estado { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public Guid? ModeradorAsignadoId { get; private set; }

    public DateTimeOffset? TomadoEn { get; private set; }

    public DateTimeOffset? ResueltoEn { get; private set; }

    public static Result<ReporteChat> Crear(
        Guid conversacionId,
        Guid reportanteId,
        TipoObjetivoReporteChat tipoObjetivo,
        Guid? mensajeId,
        CategoriaReporteChat categoria,
        string? detalle,
        DateTimeOffset creadoEn)
    {
        var detalleNormalizado = string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();
        var objetivoValido = tipoObjetivo == TipoObjetivoReporteChat.Mensaje
            ? mensajeId.HasValue && mensajeId.Value != Guid.Empty
            : !mensajeId.HasValue;
        if (conversacionId == Guid.Empty
            || reportanteId == Guid.Empty
            || !Enum.IsDefined(tipoObjetivo)
            || !Enum.IsDefined(categoria)
            || !objetivoValido
            || detalleNormalizado?.Length > 1000)
        {
            return Result.Fallo<ReporteChat>(new Error(
                ErroresModeracionChat.SolicitudInvalida,
                "La solicitud de reporte no es válida."));
        }

        return Result.Exito(new ReporteChat(
            conversacionId,
            reportanteId,
            tipoObjetivo,
            mensajeId,
            categoria,
            detalleNormalizado,
            creadoEn.ToUniversalTime()));
    }

    public Result Tomar(Guid moderadorId, DateTimeOffset ocurridoEn)
    {
        if (moderadorId == Guid.Empty || Estado != EstadoReporteChat.Pendiente)
        {
            return TransicionInvalida();
        }

        Estado = EstadoReporteChat.EnRevision;
        ModeradorAsignadoId = moderadorId;
        TomadoEn = ocurridoEn.ToUniversalTime();
        return Result.Exito();
    }

    public Result Liberar(Guid moderadorId)
    {
        if (!EstaAsignadoA(moderadorId))
        {
            return TransicionInvalida();
        }

        Estado = EstadoReporteChat.Pendiente;
        ModeradorAsignadoId = null;
        TomadoEn = null;
        return Result.Exito();
    }

    public Result Atender(Guid moderadorId, DateTimeOffset ocurridoEn) =>
        Resolver(EstadoReporteChat.Atendido, moderadorId, ocurridoEn);

    public Result Descartar(Guid moderadorId, DateTimeOffset ocurridoEn) =>
        Resolver(EstadoReporteChat.Descartado, moderadorId, ocurridoEn);

    private Result Resolver(
        EstadoReporteChat estado,
        Guid moderadorId,
        DateTimeOffset ocurridoEn)
    {
        if (!EstaAsignadoA(moderadorId))
        {
            return TransicionInvalida();
        }

        Estado = estado;
        ResueltoEn = ocurridoEn.ToUniversalTime();
        return Result.Exito();
    }

    private bool EstaAsignadoA(Guid moderadorId) =>
        moderadorId != Guid.Empty
        && Estado == EstadoReporteChat.EnRevision
        && ModeradorAsignadoId == moderadorId;

    private static Result TransicionInvalida() => Result.Fallo(new Error(
        ErroresModeracionChat.TransicionInvalida,
        "El reporte no está disponible para esa acción."));
}
