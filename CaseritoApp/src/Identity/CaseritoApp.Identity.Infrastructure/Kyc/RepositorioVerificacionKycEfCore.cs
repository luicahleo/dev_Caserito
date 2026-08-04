using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador EF Core de <see cref="IRepositorioVerificacionKyc"/> sobre <see cref="IdentityDbContext"/>.</summary>
public sealed class RepositorioVerificacionKycEfCore(
    IdentityDbContext db,
    IProtectorDocumentoKyc protectorDocumento) : IRepositorioVerificacionKyc
{
    public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes).FirstOrDefaultAsync(v => v.Id == usuarioId, ct);

    public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes)
            .FirstOrDefaultAsync(v => v.Solicitudes.Any(s => s.Id == solicitudId), ct);

    public void Agregar(VerificacionKyc verificacion) => db.VerificacionesKyc.Add(verificacion);

    public async Task<bool> ReservarDocumentoAsync(DocumentoKycRegistrado documento, CancellationToken ct)
    {
        var existente = await db.DocumentosKycRegistrados
            .SingleOrDefaultAsync(d => d.HuellaCi == documento.HuellaCi, ct);
        if (existente is not null)
        {
            return existente.UsuarioId == documento.UsuarioId;
        }

        db.DocumentosKycRegistrados.Add(documento);
        return true;
    }

    public async Task<DocumentoKycRegistrado?> ObtenerDocumentoPorSolicitudAsync(
        Guid solicitudId,
        CancellationToken ct)
    {
        var usuarioId = await db.Set<SolicitudKyc>()
            .Where(s => s.Id == solicitudId)
            .Select(s => EF.Property<Guid>(s, "VerificacionKycId"))
            .SingleOrDefaultAsync(ct);
        return usuarioId == Guid.Empty
            ? null
            : await db.DocumentosKycRegistrados.SingleOrDefaultAsync(d => d.UsuarioId == usuarioId, ct);
    }

    public async Task<DetalleSolicitudKycDto?> ObtenerDetalleAsync(Guid solicitudId, CancellationToken ct)
    {
        var datos = await (
            from solicitud in db.Set<SolicitudKyc>()
            let usuarioId = EF.Property<Guid>(solicitud, "VerificacionKycId")
            join usuario in db.Users on usuarioId equals usuario.Id
            join documento in db.DocumentosKycRegistrados on usuarioId equals documento.UsuarioId
            where solicitud.Id == solicitudId
            select new
            {
                Solicitud = solicitud,
                usuario.Nombres,
                usuario.Apellidos,
                Documento = documento,
            }).SingleOrDefaultAsync(ct);
        if (datos is null)
        {
            return null;
        }

        return new DetalleSolicitudKycDto(
            datos.Solicitud.Id,
            datos.Nombres,
            datos.Apellidos,
            protectorDocumento.Descifrar(datos.Documento.NumeroCiCifrado),
            datos.Documento.ComplementoCiCifrado is null
                ? null
                : protectorDocumento.Descifrar(datos.Documento.ComplementoCiCifrado),
            datos.Documento.DepartamentoExpedicion.ToString(),
            datos.Solicitud.Estado.ToString(),
            datos.Solicitud.ScoreSimilitud);
    }

    public async Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
        EstadoKyc? estado, int pagina, int tamano, CancellationToken ct)
    {
        // Se filtra/pagina sobre la entidad (traducible por EF) y se proyecta al DTO al final,
        // usando EF.Property para leer la FK sombra "VerificacionKycId" (= UsuarioId).
        var consulta = db.Set<SolicitudKyc>().AsQueryable();

        if (estado is not null)
        {
            consulta = consulta.Where(s => s.Estado == estado.Value);
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(s => s.EnviadaEn)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(s => new SolicitudKycResumenDto(
                s.Id,
                EF.Property<Guid>(s, "VerificacionKycId"),
                s.Estado.ToString(),
                s.TipoDocumento.ToString(),
                s.EnviadaEn,
                s.ResueltaEn,
                s.ScoreSimilitud,
                s.ResueltaPor))
            .ToListAsync(ct);

        return new ResultadoPaginado<SolicitudKycResumenDto>(items, pagina, tamano, total);
    }
}
