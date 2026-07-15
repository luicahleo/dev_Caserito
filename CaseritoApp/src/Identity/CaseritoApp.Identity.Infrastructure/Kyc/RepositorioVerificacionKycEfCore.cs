using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador EF Core de <see cref="IRepositorioVerificacionKyc"/> sobre <see cref="IdentityDbContext"/>.</summary>
public sealed class RepositorioVerificacionKycEfCore(IdentityDbContext db) : IRepositorioVerificacionKyc
{
    public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes).FirstOrDefaultAsync(v => v.Id == usuarioId, ct);

    public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes)
            .FirstOrDefaultAsync(v => v.Solicitudes.Any(s => s.Id == solicitudId), ct);

    public void Agregar(VerificacionKyc verificacion) => db.VerificacionesKyc.Add(verificacion);

    public async Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
        EstadoKyc? estado, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.VerificacionesKyc
            .SelectMany(v => v.Solicitudes.Select(s => new SolicitudKycResumenDto(
                s.Id, v.Id, s.Estado.ToString(), s.TipoDocumento.ToString(), s.EnviadaEn, s.ResueltaEn)));

        if (estado is not null)
        {
            var texto = estado.Value.ToString();
            consulta = consulta.Where(s => s.Estado == texto);
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(s => s.EnviadaEn)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        return new ResultadoPaginado<SolicitudKycResumenDto>(items, pagina, tamano, total);
    }
}
