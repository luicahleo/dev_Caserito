using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Puerto de persistencia del agregado <see cref="VerificacionKyc"/>.</summary>
public interface IRepositorioVerificacionKyc
{
    /// <summary>Carga la verificación de un usuario con su historial, o <c>null</c> si no existe.</summary>
    public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Carga la verificación que contiene la solicitud indicada, o <c>null</c>.</summary>
    public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Marca un agregado nuevo para inserción (persistido por el UnitOfWork behavior).</summary>
    public void Agregar(VerificacionKyc verificacion);

    /// <summary>Reserva un CI para su propietario o informa conflicto con otra cuenta.</summary>
    public Task<bool> ReservarDocumentoAsync(DocumentoKycRegistrado documento, CancellationToken ct) =>
        Task.FromResult(true);

    public Task<DocumentoKycRegistrado?> ObtenerDocumentoPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult<DocumentoKycRegistrado?>(null);

    public Task<DetalleSolicitudKycDto?> ObtenerDetalleAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult<DetalleSolicitudKycDto?>(null);

    /// <summary>Lista paginada de solicitudes (metadatos), filtrable por estado, más recientes primero.</summary>
    public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
        EstadoKyc? estado, int pagina, int tamano, CancellationToken ct);
}
