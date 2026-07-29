using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador de solo lectura de <see cref="IConsultaVerificacionKyc"/>.</summary>
public sealed class ConsultaVerificacionKycEfCore(IdentityDbContext db) : IConsultaVerificacionKyc
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) =>
        db.VerificacionesKyc
            .Where(v => v.Id == usuarioId)
            .SelectMany(v => v.Solicitudes)
            .AnyAsync(s => s.Estado == EstadoKyc.Aprobada, ct);

    public Task<UsuarioKycDto?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => new UsuarioKycDto(u.Email!, u.Nombre))
            .FirstOrDefaultAsync(ct);
}
