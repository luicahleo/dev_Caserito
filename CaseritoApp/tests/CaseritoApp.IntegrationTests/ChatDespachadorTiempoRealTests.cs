using CaseritoApp.Chat.Infrastructure.TiempoReal;
using CaseritoApp.Host.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatDespachadorTiempoRealTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] _propiedadesEsperadas =
        ["ConversacionId", "EnviadoEn", "Id", "RemitenteId", "Secuencia", "Texto"];

    [Fact]
    public void Contrato_mensaje_tiempo_real_tiene_exactamente_seis_campos()
    {
        var propiedades = typeof(MensajeTiempoRealDto).GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(_propiedadesEsperadas, propiedades);
    }

    [Fact]
    public async Task Publicacion_exitosa_marca_la_entrega_como_procesada()
    {
        var almacen = new AlmacenFake(Trabajo());
        var publicador = new PublicadorFake();
        var despachador = CrearDespachador(almacen, publicador);

        await despachador.ProcesarLoteAsync(CancellationToken.None);

        Assert.Single(publicador.Publicados);
        Assert.Equal(1, almacen.Procesadas);
        Assert.Equal(0, almacen.Reprogramadas);
    }

    [Fact]
    public async Task Fallo_del_publicador_reprograma_sin_marcar_procesada()
    {
        var almacen = new AlmacenFake(Trabajo());
        var publicador = new PublicadorFake { Fallar = true };
        var despachador = CrearDespachador(almacen, publicador);

        await despachador.ProcesarLoteAsync(CancellationToken.None);

        Assert.Equal(0, almacen.Procesadas);
        Assert.Equal(1, almacen.Reprogramadas);
    }

    private static DespachadorEntregasTiempoReal CrearDespachador(
        IAlmacenEntregasTiempoReal almacen,
        IPublicadorMensajesTiempoReal publicador)
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => almacen);
        servicios.AddScoped(_ => publicador);
        var proveedor = servicios.BuildServiceProvider();
        return new DespachadorEntregasTiempoReal(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OpcionesTiempoRealChat()));
    }

    private static EntregaTiempoRealReclamada Trabajo() => new(
        Guid.NewGuid(),
        _ahora.AddMinutes(1),
        0,
        new MensajeEntregaTiempoReal(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "Contenido", _ahora));

    private sealed class AlmacenFake(params EntregaTiempoRealReclamada[] trabajos)
        : IAlmacenEntregasTiempoReal
    {
        public int Procesadas { get; private set; }

        public int Reprogramadas { get; private set; }

        public Task<IReadOnlyList<EntregaTiempoRealReclamada>> ReclamarAsync(
            int cantidadMaxima, TimeSpan duracionLease, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EntregaTiempoRealReclamada>>(trabajos);

        public Task<bool> MarcarProcesadaAsync(
            EntregaTiempoRealReclamada entrega, CancellationToken cancellationToken)
        {
            Procesadas++;
            return Task.FromResult(true);
        }

        public Task<bool> ReprogramarAsync(
            EntregaTiempoRealReclamada entrega, CancellationToken cancellationToken)
        {
            Reprogramadas++;
            return Task.FromResult(true);
        }
    }

    private sealed class PublicadorFake : IPublicadorMensajesTiempoReal
    {
        public bool Fallar { get; init; }

        public List<MensajeTiempoRealDto> Publicados { get; } = [];

        public Task PublicarAsync(MensajeTiempoRealDto mensaje, CancellationToken cancellationToken)
        {
            if (Fallar)
            {
                throw new InvalidOperationException("Fallo técnico de prueba.");
            }

            Publicados.Add(mensaje);
            return Task.CompletedTask;
        }
    }
}
