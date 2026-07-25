using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure.Resenas;

public sealed class ConsultaResenasEfCore(ReputationDbContext db) : IConsultaResenas
{
    public async Task<EstadoPersistidoResenas> ObtenerEstadoAsync(
        Guid orderId,
        Guid autorId,
        Guid contraparteId,
        CancellationToken ct)
    {
        var propia = await db.Reviews
            .AsNoTracking()
            .Where(resena => resena.OrderId == orderId && resena.AutorId == autorId)
            .Select(resena => (DateTimeOffset?)resena.CreadaEn)
            .FirstOrDefaultAsync(ct);
        var contraparte = await db.Reviews
            .AsNoTracking()
            .AnyAsync(
                resena => resena.OrderId == orderId
                    && resena.AutorId == contraparteId
                    && resena.DestinatarioId == autorId,
                ct);
        return new EstadoPersistidoResenas(propia.HasValue, contraparte, propia);
    }

    public async Task<ResumenReputacionDto> ObtenerResumenAsync(
        Guid usuarioId,
        CancellationToken ct)
    {
        var publicas = PublicasRecibidas(usuarioId);
        var total = await publicas.CountAsync(ct);
        if (total == 0)
        {
            return new ResumenReputacionDto(null, 0);
        }

        var promedio = await publicas.AverageAsync(
            resena => (decimal)resena.Puntuacion,
            ct);
        return new ResumenReputacionDto(
            Math.Round(promedio, 1, MidpointRounding.AwayFromZero),
            total);
    }

    public async Task<ResultadoPaginadoResenasDto> ListarPublicasAsync(
        Guid usuarioId,
        int pagina,
        int tamano,
        CancellationToken ct)
    {
        var consulta = PublicasRecibidas(usuarioId);
        var total = await consulta.CountAsync(ct);
        var items = await consulta
            .OrderByDescending(resena => resena.CreadaEn)
            .ThenByDescending(resena => resena.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(resena => new ResenaPublicaDto(
                resena.Puntuacion,
                resena.Comentario,
                resena.CreadaEn,
                resena.RolAutor == RolAutorResena.Comprador
                    ? "comprador"
                    : "vendedor"))
            .ToListAsync(ct);
        return new ResultadoPaginadoResenasDto(items, pagina, tamano, total);
    }

    private IQueryable<Resena> PublicasRecibidas(Guid usuarioId) =>
        db.Reviews
            .AsNoTracking()
            .Where(resena => resena.DestinatarioId == usuarioId
                && db.Reviews.Any(par => par.OrderId == resena.OrderId
                    && par.AutorId == resena.DestinatarioId
                    && par.DestinatarioId == resena.AutorId));
}
