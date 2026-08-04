using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
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

    public async Task<bool> PuedeEditarIdentidadAsync(Guid usuarioId, CancellationToken ct) =>
        !await db.VerificacionesKyc
            .Where(v => v.Id == usuarioId)
            .SelectMany(v => v.Solicitudes)
            .AnyAsync(s => s.Estado == EstadoKyc.Pendiente || s.Estado == EstadoKyc.Aprobada, ct);

    public async Task<bool> EstaHabilitadoParaMarketplaceAsync(Guid usuarioId, CancellationToken ct)
    {
        if (await EstaVerificadoAsync(usuarioId, ct))
        {
            return true;
        }

        return await (
            from usuarioRol in db.UserRoles
            join rol in db.Roles on usuarioRol.RoleId equals rol.Id
            where usuarioRol.UserId == usuarioId && rol.Name == RolesApp.AdminPlataforma
            select usuarioRol).AnyAsync(ct);
    }

    public Task<UsuarioKycDto?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => new UsuarioKycDto(u.Email!, u.Nombres))
            .FirstOrDefaultAsync(ct);
}
