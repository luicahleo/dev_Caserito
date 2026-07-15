using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Raíz de agregado de verificación de identidad de un usuario. Su <see cref="Entity.Id"/> es el id
/// del usuario. Agrupa el historial de solicitudes; el estado efectivo es el de la última solicitud.
/// </summary>
public sealed class VerificacionKyc : AggregateRoot
{
    private readonly List<SolicitudKyc> _solicitudes = [];

    // Constructor para EF Core.
    private VerificacionKyc()
    {
    }

    private VerificacionKyc(Guid usuarioId) : base(usuarioId)
    {
    }

    /// <summary>Crea el agregado de verificación para el usuario indicado.</summary>
    public static VerificacionKyc Crear(Guid usuarioId) => new(usuarioId);

    /// <summary>Id del usuario dueño de la verificación (coincide con <see cref="Entity.Id"/>).</summary>
    public Guid UsuarioId => Id;

    /// <summary>Historial de solicitudes (orden de inserción).</summary>
    public IReadOnlyCollection<SolicitudKyc> Solicitudes => _solicitudes.AsReadOnly();

    /// <summary>Última solicitud enviada, o <c>null</c> si no hay ninguna.</summary>
    public SolicitudKyc? SolicitudActual =>
        _solicitudes.Count == 0 ? null : _solicitudes.OrderBy(s => s.EnviadaEn).Last();

    /// <summary>El usuario está verificado si alguna solicitud fue aprobada.</summary>
    public bool EstaVerificado => _solicitudes.Any(s => s.Estado == EstadoKyc.Aprobada);

    /// <summary>Registra una nueva solicitud si no hay una pendiente ni una ya aprobada.</summary>
    public Result<SolicitudKyc> EnviarSolicitud(
        string referenciaDocumento, string referenciaSelfie, TipoDocumento tipo, DateTimeOffset cuando)
    {
        if (EstaVerificado)
        {
            return Result.Fallo<SolicitudKyc>(
                new Error(ErroresKyc.YaVerificado, "El usuario ya está verificado."));
        }

        if (SolicitudActual?.Estado == EstadoKyc.Pendiente)
        {
            return Result.Fallo<SolicitudKyc>(
                new Error(ErroresKyc.SolicitudPendienteExiste, "Ya existe una solicitud pendiente."));
        }

        var solicitud = new SolicitudKyc(referenciaDocumento, referenciaSelfie, tipo, cuando);
        _solicitudes.Add(solicitud);
        return Result.Exito(solicitud);
    }

    /// <summary>Aprueba una solicitud pendiente.</summary>
    public Result Aprobar(Guid solicitudId, Guid revisorId, DateTimeOffset cuando) =>
        Resolver(solicitudId, s => s.MarcarAprobada(revisorId, cuando));

    /// <summary>Rechaza una solicitud pendiente con un motivo.</summary>
    public Result Rechazar(Guid solicitudId, Guid revisorId, string motivo, DateTimeOffset cuando) =>
        Resolver(solicitudId, s => s.MarcarRechazada(revisorId, motivo, cuando));

    private Result Resolver(Guid solicitudId, Action<SolicitudKyc> transicion)
    {
        var solicitud = _solicitudes.FirstOrDefault(s => s.Id == solicitudId);
        if (solicitud is null)
        {
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        if (solicitud.Estado != EstadoKyc.Pendiente)
        {
            return Result.Fallo(new Error(
                ErroresKyc.TransicionInvalida, "Solo puede resolverse una solicitud pendiente."));
        }

        transicion(solicitud);
        return Result.Exito();
    }
}
