using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Seguridad;

namespace CaseritoApp.UnitTests.Chat;

public sealed class SeguridadConversacionHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cerrar_permite_al_participante()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var handler = new CerrarConversacionCommandHandler(
            new ConversacionesFake(conversacion), new BloqueosFake(), new RelojFijo(_ahora));

        var resultado = await handler.Handle(
            new CerrarConversacionCommand(conversacion.Id, conversacion.CompradorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Cerrada, conversacion.Estado);
    }

    [Fact]
    public async Task Reabrir_permite_al_participante()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        conversacion.CerrarPorParticipante(conversacion.VendedorId, _ahora);
        var handler = new ReabrirConversacionCommandHandler(
            new ConversacionesFake(conversacion), new BloqueosFake(), new RelojFijo(_ahora.AddMinutes(1)));

        var resultado = await handler.Handle(
            new ReabrirConversacionCommand(conversacion.Id, conversacion.CompradorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public async Task Cerrar_rechaza_cuando_existe_bloqueo_en_cualquier_direccion()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var handler = new CerrarConversacionCommandHandler(
            new ConversacionesFake(conversacion),
            new BloqueosFake(existeEntre: true),
            new RelojFijo(_ahora));

        var resultado = await handler.Handle(
            new CerrarConversacionCommand(conversacion.Id, conversacion.CompradorId),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Dto_expone_estado_origen_y_capacidad_sin_direccion_del_bloqueo()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        conversacion.CerrarPorParticipante(conversacion.CompradorId, _ahora);

        var dto = ConversacionDto.Desde(conversacion, puedeEnviar: false);

        Assert.Equal(EstadoConversacion.Cerrada, dto.Estado);
        Assert.Equal("Participante", dto.OrigenCierre);
        Assert.False(dto.PuedeEnviar);
        Assert.DoesNotContain(dto.GetType().GetProperties(), p => p.Name.Contains("Bloqueador", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reabrir_rechaza_cuando_existe_bloqueo_en_cualquier_direccion()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        conversacion.CerrarPorParticipante(conversacion.CompradorId, _ahora);
        var handler = new ReabrirConversacionCommandHandler(
            new ConversacionesFake(conversacion),
            new BloqueosFake(existeEntre: true),
            new RelojFijo(_ahora.AddMinutes(1)));

        var resultado = await handler.Handle(
            new ReabrirConversacionCommand(conversacion.Id, conversacion.VendedorId),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
        Assert.Equal(EstadoConversacion.Cerrada, conversacion.Estado);
    }

    [Fact]
    public async Task Cierre_no_distingue_conversacion_ausente_de_ajena()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var ausente = await new CerrarConversacionCommandHandler(
            new ConversacionesFake(null), new BloqueosFake(), new RelojFijo(_ahora)).Handle(
                new CerrarConversacionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        var ajena = await new CerrarConversacionCommandHandler(
            new ConversacionesFake(conversacion), new BloqueosFake(), new RelojFijo(_ahora)).Handle(
                new CerrarConversacionCommand(conversacion.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(ausente.EsExito);
        Assert.False(ajena.EsExito);
        Assert.Equal(ErroresConversacion.NoEncontrada, ausente.Error.Code);
        Assert.Equal(ausente.Error, ajena.Error);
    }

    private sealed class ConversacionesFake(Conversacion? conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(conversacion?.Id == id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    private sealed class BloqueosFake(bool existeEntre = false) : IRepositorioBloqueosUsuario
    {
        public Task<bool> ExisteEntreAsync(Guid usuarioA, Guid usuarioB, CancellationToken ct) =>
            Task.FromResult(existeEntre);

        public Task<BloqueoUsuario?> ObtenerAsync(
            Guid bloqueadorId, Guid bloqueadoId, CancellationToken ct) =>
            Task.FromResult<BloqueoUsuario?>(null);

        public void Agregar(BloqueoUsuario bloqueo)
        {
        }

        public void Quitar(BloqueoUsuario bloqueo)
        {
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
