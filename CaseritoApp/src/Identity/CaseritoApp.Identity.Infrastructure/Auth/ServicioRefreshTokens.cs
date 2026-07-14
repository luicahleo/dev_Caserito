using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Emite, rota y revoca refresh tokens persistidos hasheados (SHA-256), nunca en claro.</summary>
public interface IServicioRefreshTokens
{
    /// <summary>Emite un nuevo refresh token para el usuario y devuelve el token en claro (solo aquí).</summary>
    public Task<string> EmitirAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Rota un refresh token válido: lo revoca y emite uno nuevo, devolviendo el nuevo token en claro
    /// junto con el <c>userId</c> (para emitir el nuevo access JWT). Devuelve <c>null</c> si el token
    /// es inválido, expirado o ya revocado; en este último caso (reuso) revoca todos los tokens activos
    /// del usuario como medida defensiva.
    /// </summary>
    public Task<(string tokenPlano, Guid userId)?> RotarAsync(string tokenPlano, CancellationToken ct);

    /// <summary>Revoca (invalida) el refresh token indicado, si existe y está activo.</summary>
    public Task RevocarAsync(string tokenPlano, CancellationToken ct);
}

/// <inheritdoc cref="IServicioRefreshTokens"/>
public sealed class ServicioRefreshTokens(IdentityDbContext db, TimeProvider tiempo) : IServicioRefreshTokens
{
    private const int DiasVida = 7;

    /// <inheritdoc/>
    public async Task<string> EmitirAsync(Guid userId, CancellationToken ct)
    {
        var (plano, hash) = GenerarToken();
        var ahora = tiempo.GetUtcNow();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreadoEn = ahora,
            ExpiraEn = ahora.AddDays(DiasVida),
        });
        await db.SaveChangesAsync(ct);
        return plano;
    }

    /// <inheritdoc/>
    public async Task<(string tokenPlano, Guid userId)?> RotarAsync(string tokenPlano, CancellationToken ct)
    {
        var hash = Hash(tokenPlano);
        var actual = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var ahora = tiempo.GetUtcNow();

        if (actual is null)
        {
            return null;
        }

        if (!actual.EsActivo(ahora))
        {
            // Reuso de un token revocado/expirado: revoca todos los activos del usuario (defensa).
            await db.RefreshTokens
                .Where(t => t.UserId == actual.UserId && t.RevocadoEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevocadoEn, ahora), ct);
            return null;
        }

        var (nuevoPlano, nuevoHash) = GenerarToken();
        actual.RevocadoEn = ahora;
        actual.ReemplazadoPorHash = nuevoHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = actual.UserId,
            TokenHash = nuevoHash,
            CreadoEn = ahora,
            ExpiraEn = ahora.AddDays(DiasVida),
        });
        await db.SaveChangesAsync(ct);
        return (nuevoPlano, actual.UserId);
    }

    /// <inheritdoc/>
    public async Task RevocarAsync(string tokenPlano, CancellationToken ct)
    {
        var hash = Hash(tokenPlano);
        var actual = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (actual is { RevocadoEn: null })
        {
            actual.RevocadoEn = tiempo.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    private static (string plano, string hash) GenerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plano = Convert.ToBase64String(bytes);
        return (plano, Hash(plano));
    }

    private static string Hash(string plano) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plano)));
}
