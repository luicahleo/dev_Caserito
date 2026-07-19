using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Domain.Moderacion;

/// <summary>Reporte comunitario de un aviso.</summary>
public sealed class ReporteAviso : Entity
{
    // Constructor para EF Core.
#pragma warning disable S1144
    private ReporteAviso()
    {
        Detalle = null;
    }
#pragma warning restore S1144

    private ReporteAviso(
        Guid avisoId,
        Guid reportanteId,
        MotivoReporteAviso motivo,
        string? detalle,
        DateTime fechaCreacion)
    {
        AvisoId = avisoId;
        ReportanteId = reportanteId;
        Motivo = motivo;
        Detalle = detalle;
        Estado = EstadoReporteAviso.Pendiente;
        FechaCreacion = fechaCreacion;
    }

    public Guid AvisoId { get; private set; }
    public Guid ReportanteId { get; private set; }
    public MotivoReporteAviso Motivo { get; private set; }
    public string? Detalle { get; private set; }
    public EstadoReporteAviso Estado { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaResolucion { get; private set; }
    public Guid? ResueltoPorId { get; private set; }

    public static ReporteAviso Crear(
        Guid avisoId,
        Guid reportanteId,
        MotivoReporteAviso motivo,
        string? detalle,
        DateTime fechaCreacion) =>
        new(avisoId, reportanteId, motivo, detalle, fechaCreacion);

    public Result Atender(Guid? moderadorId, DateTime fechaResolucion) =>
        Resolver(EstadoReporteAviso.Atendido, moderadorId, fechaResolucion);

    public Result Descartar(Guid moderadorId, DateTime fechaResolucion) =>
        Resolver(EstadoReporteAviso.Descartado, moderadorId, fechaResolucion);

    private Result Resolver(EstadoReporteAviso estado, Guid? moderadorId, DateTime fechaResolucion)
    {
        if (Estado != EstadoReporteAviso.Pendiente)
        {
            return Result.Fallo(new Error(ErroresAviso.ReporteYaResuelto, "El reporte ya fue resuelto."));
        }

        Estado = estado;
        ResueltoPorId = moderadorId;
        FechaResolucion = fechaResolucion;
        return Result.Exito();
    }
}
