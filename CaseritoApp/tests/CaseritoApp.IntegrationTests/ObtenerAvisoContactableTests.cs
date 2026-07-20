using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Catalog.Infrastructure.Avisos;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ObtenerAvisoContactableTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly DateTime _ahora = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Solo_aviso_activo_y_visible_expone_referencia_contactable()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var activo = NuevoAviso();
        var pausado = NuevoAviso();
        pausado.Pausar(_ahora.AddMinutes(1));
        var oculto = NuevoAviso();
        oculto.OcultarPorModeracion(_ahora.AddMinutes(1));
        var eliminado = NuevoAviso();
        eliminado.Eliminar(_ahora.AddMinutes(1));
        db.Avisos.AddRange(activo, pausado, oculto, eliminado);
        await db.SaveChangesAsync();
        var consulta = new ConsultaAvisosPublicaEfCore(db);

        var referencia = await consulta.ObtenerReferenciaContactableAsync(activo.Id, CancellationToken.None);

        Assert.NotNull(referencia);
        Assert.Equal(activo.Id, referencia.AvisoId);
        Assert.Equal(activo.VendedorId, referencia.VendedorId);
        Assert.Null(await consulta.ObtenerReferenciaContactableAsync(pausado.Id, CancellationToken.None));
        Assert.Null(await consulta.ObtenerReferenciaContactableAsync(oculto.Id, CancellationToken.None));
        Assert.Null(await consulta.ObtenerReferenciaContactableAsync(eliminado.Id, CancellationToken.None));
        Assert.Null(await consulta.ObtenerReferenciaContactableAsync(Guid.NewGuid(), CancellationToken.None));

        var adaptador = scope.ServiceProvider.GetRequiredService<IConsultaAvisoContactable>();
        var referenciaChat = await adaptador.ObtenerAsync(activo.Id, CancellationToken.None);
        Assert.Equal(referencia.AvisoId, referenciaChat!.AvisoId);
        Assert.Equal(referencia.VendedorId, referenciaChat.VendedorId);
    }

    private static Aviso NuevoAviso() => Aviso.Crear(
        Guid.NewGuid(),
        "Aviso",
        "Descripción",
        Dinero.Crear(100, Moneda.BOB).Valor,
        Guid.NewGuid(),
        Guid.NewGuid(),
        CondicionArticulo.Usado,
        _ahora);
}
