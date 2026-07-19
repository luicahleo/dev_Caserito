using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Moderacion;

/// <summary>Registro append-only y sin PII de una acción de moderación.</summary>
public sealed class RegistroModeracion : Entity
{
    // Constructor para EF Core.
#pragma warning disable S1144
    private RegistroModeracion()
    {
    }
#pragma warning restore S1144

    private RegistroModeracion(
        Guid avisoId,
        Guid moderadorId,
        AccionModeracionAviso accion,
        Guid? reporteId,
        DateTime fecha)
    {
        AvisoId = avisoId;
        ModeradorId = moderadorId;
        Accion = accion;
        ReporteId = reporteId;
        Fecha = fecha;
    }

    public Guid AvisoId { get; private set; }
    public Guid ModeradorId { get; private set; }
    public AccionModeracionAviso Accion { get; private set; }
    public Guid? ReporteId { get; private set; }
    public DateTime Fecha { get; private set; }

    public static RegistroModeracion Crear(
        Guid avisoId,
        Guid moderadorId,
        AccionModeracionAviso accion,
        Guid? reporteId,
        DateTime fecha) =>
        new(avisoId, moderadorId, accion, reporteId, fecha);
}
