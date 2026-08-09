using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class MarcarLecturaCommandHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class RepositorioFake(Conversacion? conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(conversacion?.Id == id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    [Fact]
    public async Task Participante_avanza_lectura_con_reloj_de_servidor()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        conversacion.CrearMensaje(vendedorId, Guid.NewGuid(), 8, "Hola", _ahora.AddMinutes(1));
        conversacion.LimpiarEventos();
        var handler = new MarcarLecturaCommandHandler(
            new RepositorioFake(conversacion), new RelojFijo(_ahora.AddMinutes(2)));

        var resultado = await handler.Handle(
            new MarcarLecturaCommand(conversacion.Id, compradorId, 8), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(vendedorId, resultado.Valor.DestinatarioEstadoId);
        Assert.Equal(8, resultado.Valor.UltimaSecuenciaEntregada);
        Assert.Equal(8, resultado.Valor.UltimaSecuenciaLeida);
        Assert.Equal(8, conversacion.UltimaSecuenciaLeidaComprador);
        var evento = Assert.IsType<LecturaAvanzada>(Assert.Single(conversacion.EventosDeDominio));
        Assert.Equal(_ahora.AddMinutes(2), evento.OcurridoEn);
    }

    [Fact]
    public async Task Ausente_y_tercero_devuelven_no_encontrado()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var comando = new MarcarLecturaCommand(conversacion.Id, Guid.NewGuid(), 0);

        var ausente = await new MarcarLecturaCommandHandler(
            new RepositorioFake(null), TimeProvider.System).Handle(comando, CancellationToken.None);
        var tercero = await new MarcarLecturaCommandHandler(
            new RepositorioFake(conversacion), TimeProvider.System).Handle(comando, CancellationToken.None);

        Assert.Equal(ErroresConversacion.NoEncontrada, ausente.Error.Code);
        Assert.Equal(ausente.Error.Code, tercero.Error.Code);
    }

    [Fact]
    public void Validator_rechaza_ids_vacios_y_secuencia_negativa()
    {
        var resultado = new MarcarLecturaCommandValidator().Validate(
            new MarcarLecturaCommand(Guid.Empty, Guid.Empty, -1));

        Assert.False(resultado.IsValid);
        Assert.Equal(3, resultado.Errors.Count);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
