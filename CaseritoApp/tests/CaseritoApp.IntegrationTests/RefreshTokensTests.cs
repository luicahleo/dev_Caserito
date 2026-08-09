using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica, contra un SQL Server real (Testcontainers), la emisión, rotación y revocación de
/// refresh tokens, incluyendo la detección de reuso de un token ya revocado.
/// </summary>
public sealed class RefreshTokensTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static async Task<Guid> CrearUsuarioAsync(IServiceProvider servicios)
    {
        var userManager = servicios.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = new ApplicationUser
        {
            UserName = $"refresh-{Guid.NewGuid():N}@caserito.test",
            Email = $"refresh-{Guid.NewGuid():N}@caserito.test",
            Nombres = "Usuario de prueba",
            CiudadId = new Guid("22222222-2222-2222-2222-000000000001"),
        };

        var resultado = await userManager.CreateAsync(usuario, "Password123!");
        Assert.True(resultado.Succeeded, string.Join(", ", resultado.Errors.Select(e => e.Description)));

        return usuario.Id;
    }

    [Fact]
    public async Task Emitir_crea_un_refresh_token_activo_persistido_hasheado()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IServicioRefreshTokens>();
        var tiempo = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var userId = await CrearUsuarioAsync(scope.ServiceProvider);

        var tokenPlano = await servicio.EmitirAsync(userId, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(tokenPlano));

        var persistido = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId);
        Assert.NotEqual(tokenPlano, persistido.TokenHash); // el token plano nunca se persiste
        Assert.True(persistido.EsActivo(tiempo.GetUtcNow()));
        Assert.Equal(TimeSpan.FromDays(30), persistido.ExpiraEn - persistido.CreadoEn);
    }

    [Fact]
    public async Task Rotar_devuelve_nuevo_token_e_invalida_el_anterior()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IServicioRefreshTokens>();
        var tiempo = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var userId = await CrearUsuarioAsync(scope.ServiceProvider);
        var original = await servicio.EmitirAsync(userId, CancellationToken.None);

        var resultado = await servicio.RotarAsync(original, CancellationToken.None);

        Assert.NotNull(resultado);
        var (nuevoPlano, userIdDevuelto) = resultado.Value;
        Assert.Equal(userId, userIdDevuelto);
        Assert.NotEqual(original, nuevoPlano);

        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        Assert.Equal(2, tokens.Count);

        var ahora = tiempo.GetUtcNow();
        var revocados = tokens.Where(t => !t.EsActivo(ahora)).ToList();
        var activos = tokens.Where(t => t.EsActivo(ahora)).ToList();
        Assert.Single(revocados);
        Assert.Single(activos);
        Assert.NotNull(revocados[0].ReemplazadoPorHash);
        Assert.Equal(TimeSpan.FromDays(30), activos[0].ExpiraEn - activos[0].CreadoEn);
    }

    [Fact]
    public async Task Rotar_con_token_ya_revocado_detecta_reuso_y_revoca_todos_los_activos()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IServicioRefreshTokens>();
        var tiempo = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var userId = await CrearUsuarioAsync(scope.ServiceProvider);
        var original = await servicio.EmitirAsync(userId, CancellationToken.None);

        var primeraRotacion = await servicio.RotarAsync(original, CancellationToken.None);
        Assert.NotNull(primeraRotacion);
        var nuevoActivo = primeraRotacion!.Value.tokenPlano;

        // Reuso: se intenta rotar de nuevo el token original, ya revocado por la primera rotación.
        var segundaRotacion = await servicio.RotarAsync(original, CancellationToken.None);

        Assert.Null(segundaRotacion);

        var ahora = tiempo.GetUtcNow();
        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        Assert.All(tokens, t => Assert.False(t.EsActivo(ahora)));

        // El token que era el válido tras la primera rotación también queda revocado (defensa).
        var elActivoTrasPrimeraRotacion = tokens.Single(t => t.TokenHash == HashDePrueba(nuevoActivo));
        Assert.False(elActivoTrasPrimeraRotacion.EsActivo(ahora));
    }

    [Fact]
    public async Task Revocar_invalida_el_token()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var servicio = scope.ServiceProvider.GetRequiredService<IServicioRefreshTokens>();
        var tiempo = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var userId = await CrearUsuarioAsync(scope.ServiceProvider);
        var tokenPlano = await servicio.EmitirAsync(userId, CancellationToken.None);

        await servicio.RevocarAsync(tokenPlano, CancellationToken.None);

        var persistido = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId);
        Assert.False(persistido.EsActivo(tiempo.GetUtcNow()));

        // Rotar con un token ya revocado por RevocarAsync también debe fallar (null).
        var rotacion = await servicio.RotarAsync(tokenPlano, CancellationToken.None);
        Assert.Null(rotacion);
    }

    [Fact]
    public async Task Revocar_todas_invalida_solo_los_tokens_del_usuario_indicado()
    {
        using var scope = factory.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IServicioRefreshTokens>();
        var revocador = scope.ServiceProvider.GetRequiredService<IRevocadorSesionesUsuario>();
        var usuarioA = await CrearUsuarioAsync(scope.ServiceProvider);
        var usuarioB = await CrearUsuarioAsync(scope.ServiceProvider);
        var tokenA1 = await servicio.EmitirAsync(usuarioA, CancellationToken.None);
        var tokenA2 = await servicio.EmitirAsync(usuarioA, CancellationToken.None);
        var tokenB = await servicio.EmitirAsync(usuarioB, CancellationToken.None);

        await revocador.RevocarTodasAsync(usuarioA, CancellationToken.None);

        Assert.Null(await servicio.RotarAsync(tokenA1, CancellationToken.None));
        Assert.Null(await servicio.RotarAsync(tokenA2, CancellationToken.None));
        Assert.NotNull(await servicio.RotarAsync(tokenB, CancellationToken.None));
    }

    private static string HashDePrueba(string plano) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plano)));
}
