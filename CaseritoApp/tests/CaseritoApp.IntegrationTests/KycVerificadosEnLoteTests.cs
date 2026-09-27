using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Consulta en lote usada por el listado público para marcar vendedores verificados
/// sin caer en N+1.
/// </summary>
public sealed class KycVerificadosEnLoteTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private async Task<Guid> SembrarUsuarioConKycAsync(bool aprobado)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var usuarioId = Guid.NewGuid();
        var verificacion = VerificacionKyc.Crear(usuarioId);
        var envio = verificacion.EnviarSolicitud(
            "doc-lote", "selfie-lote", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        if (aprobado)
        {
            verificacion.Aprobar(envio.Valor.Id, SistemaActor.Id, DateTimeOffset.UtcNow);
        }

        db.VerificacionesKyc.Add(verificacion);
        await db.SaveChangesAsync();
        return usuarioId;
    }

    [Fact]
    public async Task Devuelve_solo_los_usuarios_con_kyc_aprobado()
    {
        var aprobado1 = await SembrarUsuarioConKycAsync(aprobado: true);
        var aprobado2 = await SembrarUsuarioConKycAsync(aprobado: true);
        var pendiente = await SembrarUsuarioConKycAsync(aprobado: false);

        using var scope = factory.Services.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();

        var verificados = await consulta.ObtenerVerificadosAsync(
            [aprobado1, aprobado2, pendiente], CancellationToken.None);

        Assert.Contains(aprobado1, verificados);
        Assert.Contains(aprobado2, verificados);
        Assert.DoesNotContain(pendiente, verificados);
    }

    [Fact]
    public async Task Con_coleccion_vacia_devuelve_conjunto_vacio()
    {
        using var scope = factory.Services.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();

        var verificados = await consulta.ObtenerVerificadosAsync([], CancellationToken.None);

        Assert.Empty(verificados);
    }
}
