using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Chat.Infrastructure.TiempoReal;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatOutboxTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly DateTimeOffset _inicio = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] _propiedadesMensajeEsperadas =
        ["ConversacionId", "EnviadoEn", "Id", "RemitenteId", "Secuencia", "Texto"];

    [Fact]
    public async Task Reclamantes_concurrentes_no_reciben_la_misma_entrega()
    {
        await CrearEntregasAsync(1);
        var reloj = new RelojPruebas(_inicio.AddHours(1));

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var almacenA = new AlmacenEntregasTiempoRealSql(
            scopeA.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);
        var almacenB = new AlmacenEntregasTiempoRealSql(
            scopeB.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);

        var resultados = await Task.WhenAll(
            almacenA.ReclamarAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None),
            almacenB.ReclamarAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Single(resultados.SelectMany(x => x));
    }

    [Fact]
    public async Task Reclamar_respeta_orden_estable_y_proyecta_solo_el_mensaje_necesario()
    {
        await CrearEntregasAsync(3, 1, 2);
        var reloj = new RelojPruebas(_inicio.AddHours(1));
        using var scope = factory.Services.CreateScope();
        var almacen = new AlmacenEntregasTiempoRealSql(
            scope.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);

        var trabajos = await almacen.ReclamarAsync(3, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal([1L, 2L, 3L], trabajos.Select(x => x.Mensaje.Secuencia));
        var propiedades = typeof(MensajeEntregaTiempoReal).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(_propiedadesMensajeEsperadas, propiedades);
    }

    [Fact]
    public async Task Lease_vencida_vuelve_a_ser_reclamable()
    {
        await CrearEntregasAsync(1);
        var reloj = new RelojPruebas(_inicio.AddHours(1));
        using var primerScope = factory.Services.CreateScope();
        var primerAlmacen = new AlmacenEntregasTiempoRealSql(
            primerScope.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);
        var primera = Assert.Single(await primerAlmacen.ReclamarAsync(
            1, TimeSpan.FromMinutes(1), CancellationToken.None));
        reloj.Avanzar(TimeSpan.FromMinutes(2));
        using var segundoScope = factory.Services.CreateScope();
        var segundoAlmacen = new AlmacenEntregasTiempoRealSql(
            segundoScope.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);

        var segunda = Assert.Single(await segundoAlmacen.ReclamarAsync(
            1, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Equal(primera.EntregaId, segunda.EntregaId);
        Assert.NotEqual(primera.LeaseHasta, segunda.LeaseHasta);
    }

    [Fact]
    public async Task Exito_marca_procesada_si_la_lease_sigue_vigente()
    {
        await CrearEntregasAsync(1);
        var reloj = new RelojPruebas(_inicio.AddHours(1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var almacen = new AlmacenEntregasTiempoRealSql(db, reloj);
        var trabajo = Assert.Single(await almacen.ReclamarAsync(
            1, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.True(await almacen.MarcarProcesadaAsync(trabajo, CancellationToken.None));

        db.ChangeTracker.Clear();
        var entrega = await db.EntregasTiempoReal.SingleAsync(x => x.Id == trabajo.EntregaId);
        Assert.Equal(reloj.GetUtcNow(), entrega.ProcesadaEn);
        Assert.Null(entrega.LeaseHasta);
    }

    [Fact]
    public async Task Fallo_incrementa_intentos_libera_lease_y_programa_backoff_acotado()
    {
        await CrearEntregasAsync(1);
        var reloj = new RelojPruebas(_inicio.AddHours(1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var almacen = new AlmacenEntregasTiempoRealSql(db, reloj);
        var trabajo = Assert.Single(await almacen.ReclamarAsync(
            1, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.True(await almacen.ReprogramarAsync(trabajo, CancellationToken.None));

        db.ChangeTracker.Clear();
        var entrega = await db.EntregasTiempoReal.SingleAsync(x => x.Id == trabajo.EntregaId);
        Assert.Equal(1, entrega.Intentos);
        Assert.Null(entrega.LeaseHasta);
        Assert.InRange(entrega.ProximoIntentoEn, reloj.GetUtcNow().AddSeconds(1), reloj.GetUtcNow().AddMinutes(5));
    }

    [Fact]
    public async Task Fallo_despues_de_publicar_permite_reclamar_de_nuevo_al_vencer_la_lease()
    {
        await CrearEntregasAsync(1);
        var reloj = new RelojPruebas(_inicio.AddHours(1));
        using var scope = factory.Services.CreateScope();
        var almacen = new AlmacenEntregasTiempoRealSql(
            scope.ServiceProvider.GetRequiredService<ChatDbContext>(), reloj);
        var primera = Assert.Single(await almacen.ReclamarAsync(
            1, TimeSpan.FromSeconds(10), CancellationToken.None));

        reloj.Avanzar(TimeSpan.FromSeconds(11));
        var segunda = Assert.Single(await almacen.ReclamarAsync(
            1, TimeSpan.FromSeconds(10), CancellationToken.None));

        Assert.Equal(primera.Mensaje.Id, segunda.Mensaje.Id);
        Assert.Equal(primera.Mensaje.Secuencia, segunda.Mensaje.Secuencia);
    }

    private async Task CrearEntregasAsync(params long[] secuencias)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        await db.EntregasTiempoReal.ExecuteDeleteAsync();
        await db.Mensajes.ExecuteDeleteAsync();
        await db.Conversaciones.ExecuteDeleteAsync();
        foreach (var secuencia in secuencias)
        {
            var conversacion = Conversacion.Crear(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _inicio).Valor;
            var mensaje = conversacion.CrearMensaje(
                conversacion.CompradorId,
                Guid.NewGuid(),
                secuencia,
                $"Contenido {secuencia}",
                _inicio.AddMinutes(secuencia)).Valor;
            db.Conversaciones.Add(conversacion);
            db.Mensajes.Add(mensaje);
        }

        await db.SaveChangesAsync();
    }

    private sealed class RelojPruebas(DateTimeOffset ahora) : TimeProvider
    {
        private DateTimeOffset _ahora = ahora;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan intervalo) => _ahora += intervalo;
    }
}
