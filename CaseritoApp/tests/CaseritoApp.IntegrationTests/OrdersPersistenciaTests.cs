using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Orders;
using CaseritoApp.IntegrationTests.Infrastructure;
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using CaseritoApp.Orders.Infrastructure;
using CaseritoApp.Orders.Infrastructure.Ordenes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class OrdersPersistenciaTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Persiste_y_proyecta_una_orden_sin_exponer_otro_participante()
    {
        var vendedorId = Guid.NewGuid();
        var compradorId = Guid.NewGuid();
        var orden = CrearOrden(compradorId, vendedorId);

        using var scope = factory.Services.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IRepositorioOrdenes>();
        var consultas = scope.ServiceProvider.GetRequiredService<IConsultaOrdenes>();
        repositorio.Agregar(orden);
        await scope.ServiceProvider.GetRequiredService<UnitOfWorkOrders>()
            .GuardarCambiosAsync(CancellationToken.None);

        var compras = await consultas.ListarAsync(
            compradorId, "comprador", EstadoOrden.Requested, 1, 20, CancellationToken.None);
        var detalleAjeno = await consultas.ObtenerAsync(
            orden.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Single(compras.Items);
        Assert.Equal("comprador", compras.Items[0].Rol);
        Assert.Null(detalleAjeno);
    }

    [Fact]
    public async Task Publica_el_cambio_de_estado_solo_despues_de_guardar()
    {
        var publicador = new PublicadorFake();
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(Guid.NewGuid(), vendedorId);

        await using var db = factory.CrearOrdersDbContext();
        db.Orders.Add(orden);
        var unidad = new UnitOfWorkOrders(db, publicador);
        await unidad.GuardarCambiosAsync(CancellationToken.None);
        Assert.Empty(publicador.Eventos);

        Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.Empty(publicador.Eventos);

        await unidad.GuardarCambiosAsync(CancellationToken.None);

        var evento = Assert.Single(publicador.Eventos);
        Assert.Equal(orden.Id, evento.OrderId);
        Assert.Equal("Requested", evento.OldStatus);
        Assert.Equal("Agreed", evento.NewStatus);
    }

    [Fact]
    public async Task Rechaza_orden_duplicada_por_aviso_y_comprador()
    {
        var compradorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();
        var primera = CrearOrden(compradorId, Guid.NewGuid(), avisoId);
        var segunda = CrearOrden(compradorId, Guid.NewGuid(), avisoId);

        await using var db = factory.CrearOrdersDbContext();
        var unidad = new UnitOfWorkOrders(db, new PublicadorFake());
        db.Orders.Add(primera);
        await unidad.GuardarCambiosAsync(CancellationToken.None);
        db.Orders.Add(segunda);

        await Assert.ThrowsAsync<ConflictoUnicidadOrdersException>(
            () => unidad.GuardarCambiosAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Rechaza_actualizacion_con_rowversion_obsoleta()
    {
        var vendedorId = Guid.NewGuid();
        var orden = CrearOrden(Guid.NewGuid(), vendedorId);
        await using (var dbInicial = factory.CrearOrdersDbContext())
        {
            dbInicial.Orders.Add(orden);
            await new UnitOfWorkOrders(dbInicial, new PublicadorFake())
                .GuardarCambiosAsync(CancellationToken.None);
        }

        await using var dbPrimero = factory.CrearOrdersDbContext();
        await using var dbSegundo = factory.CrearOrdersDbContext();
        var primeraCopia = await dbPrimero.Orders.FindAsync(orden.Id);
        var segundaCopia = await dbSegundo.Orders.FindAsync(orden.Id);
        Assert.NotNull(primeraCopia);
        Assert.NotNull(segundaCopia);
        Assert.True(primeraCopia.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(segundaCopia.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);

        await new UnitOfWorkOrders(dbPrimero, new PublicadorFake())
            .GuardarCambiosAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(
            () => new UnitOfWorkOrders(dbSegundo, new PublicadorFake())
                .GuardarCambiosAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Puede_resolicitar_tras_cancelar_la_orden_previa()
    {
        var avisoId = Guid.NewGuid();
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();

        await using (var db = factory.CrearOrdersDbContext())
        {
            var previa = CrearOrden(compradorId, vendedorId, avisoId);
            previa.Cancelar(compradorId, DateTimeOffset.UtcNow);
            db.Orders.Add(previa);
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CrearOrdersDbContext())
        {
            var nueva = CrearOrden(compradorId, vendedorId, avisoId);
            db.Orders.Add(nueva);

            var afectados = await db.SaveChangesAsync();

            Assert.Equal(1, afectados);
        }
    }

    [Fact]
    public async Task Rechaza_dos_ordenes_no_canceladas_para_el_mismo_aviso_y_comprador()
    {
        var avisoId = Guid.NewGuid();
        var compradorId = Guid.NewGuid();

        await using var db = factory.CrearOrdersDbContext();
        db.Orders.Add(CrearOrden(compradorId, Guid.NewGuid(), avisoId));
        await db.SaveChangesAsync();

        db.Orders.Add(CrearOrden(compradorId, Guid.NewGuid(), avisoId));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Persiste_ganadora_competidoras_y_fechas_de_cierre_en_un_commit()
    {
        var avisoId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var compradorId = Guid.NewGuid();
        var ganadora = CrearOrden(compradorId, vendedorId, avisoId);
        var competidoraSolicitada = CrearOrden(Guid.NewGuid(), vendedorId, avisoId);
        var competidoraAcordada = CrearOrden(Guid.NewGuid(), vendedorId, avisoId);
        Assert.True(ganadora.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
        Assert.True(competidoraAcordada.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);

        using var scope = factory.Services.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IRepositorioOrdenes>();
        repositorio.Agregar(ganadora);
        repositorio.Agregar(competidoraSolicitada);
        repositorio.Agregar(competidoraAcordada);
        var unidad = scope.ServiceProvider.GetRequiredService<UnitOfWorkOrders>();
        await unidad.GuardarCambiosAsync(CancellationToken.None);

        var resultado = await new MarcarOrdenVendidaCommandHandler(repositorio).Handle(
            new MarcarOrdenVendidaCommand(ganadora.Id, vendedorId),
            CancellationToken.None);
        await unidad.GuardarCambiosAsync(CancellationToken.None);

        Assert.True(resultado.EsExito);
        await using var verificacion = factory.CrearOrdersDbContext();
        var estados = await verificacion.Orders
            .Where(orden => orden.AvisoId == avisoId)
            .ToDictionaryAsync(orden => orden.Id, orden => orden.Estado);
        Assert.Equal(EstadoOrden.MarkedAsSold, estados[ganadora.Id]);
        Assert.Equal(EstadoOrden.Cancelled, estados[competidoraSolicitada.Id]);
        Assert.Equal(EstadoOrden.Cancelled, estados[competidoraAcordada.Id]);

        var consultas = new ConsultaOrdenesEfCore(verificacion);
        var detalle = await consultas.ObtenerAsync(
            ganadora.Id, compradorId, CancellationToken.None);
        Assert.NotNull(detalle);
        Assert.NotNull(detalle.MarcadaVendidaEn);
        Assert.Null(detalle.CompradorConfirmoEn);
        Assert.Null(detalle.CompletadaEn);
    }

    private static Orden CrearOrden(
        Guid compradorId,
        Guid vendedorId,
        Guid? avisoId = null)
    {
        var resultado = Orden.Crear(
            avisoId ?? Guid.NewGuid(),
            compradorId,
            vendedorId,
            125.50m,
            "BOB",
            DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        return resultado.Valor;
    }

    private sealed class PublicadorFake : IPublicadorEventosIntegracion
    {
        public List<OrderStatusChanged> Eventos { get; } = [];

        public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
        {
            if (evento is OrderStatusChanged cambio)
            {
                Eventos.Add(cambio);
            }

            return Task.CompletedTask;
        }
    }
}
