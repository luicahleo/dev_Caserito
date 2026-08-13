using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class MarcarEntregaCommandHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed class RepositorioFake(Conversacion? conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(conversacion?.Id == id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    [Fact]
    public async Task Participante_avanza_entrega_con_reloj_de_servidor()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;
        conversacion.CrearMensaje(conversacion.VendedorId, Guid.NewGuid(), 4, "Hola", _ahora);
        conversacion.LimpiarEventos();
        var handler = new MarcarEntregaCommandHandler(
            new RepositorioFake(conversacion), new RelojFijo(_ahora.AddMinutes(1)));

        var resultado = await handler.Handle(
            new MarcarEntregaCommand(conversacion.Id, compradorId, 4), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(conversacion.VendedorId, resultado.Valor.DestinatarioEstadoId);
        Assert.Equal(4, resultado.Valor.UltimaSecuenciaEntregada);
        Assert.Equal(0, resultado.Valor.UltimaSecuenciaLeida);
        Assert.Equal(4, conversacion.UltimaSecuenciaEntregadaComprador);
        var evento = Assert.IsType<EntregaAvanzada>(Assert.Single(conversacion.EventosDeDominio));
        Assert.Equal(_ahora.AddMinutes(1), evento.OcurridoEn);
    }

    [Fact]
    public async Task Ausente_y_tercero_devuelven_no_encontrado()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var comando = new MarcarEntregaCommand(conversacion.Id, Guid.NewGuid(), 0);

        var ausente = await new MarcarEntregaCommandHandler(
            new RepositorioFake(null), TimeProvider.System).Handle(comando, CancellationToken.None);
        var tercero = await new MarcarEntregaCommandHandler(
            new RepositorioFake(conversacion), TimeProvider.System).Handle(comando, CancellationToken.None);

        Assert.Equal(ErroresConversacion.NoEncontrada, ausente.Error.Code);
        Assert.Equal(ausente.Error.Code, tercero.Error.Code);
    }

    [Fact]
    public async Task Vendedor_avanza_su_cursor_y_devuelve_el_comprador()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        conversacion.CrearMensaje(compradorId, Guid.NewGuid(), 2, "Hola", _ahora);
        conversacion.LimpiarEventos();
        var handler = new MarcarEntregaCommandHandler(
            new RepositorioFake(conversacion), new RelojFijo(_ahora.AddMinutes(1)));

        var resultado = await handler.Handle(
            new MarcarEntregaCommand(conversacion.Id, vendedorId, 2), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(compradorId, resultado.Valor.DestinatarioEstadoId);
        Assert.Equal(2, resultado.Valor.UltimaSecuenciaEntregada);
        Assert.Equal(0, resultado.Valor.UltimaSecuenciaLeida);
    }

    [Fact]
    public async Task Secuencia_superior_a_la_existente_devuelve_error_de_dominio()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), _ahora).Valor;
        var handler = new MarcarEntregaCommandHandler(
            new RepositorioFake(conversacion), new RelojFijo(_ahora));

        var resultado = await handler.Handle(
            new MarcarEntregaCommand(conversacion.Id, compradorId, 1), CancellationToken.None);

        Assert.False(resultado.EsExito);
    }

    [Fact]
    public void Validator_rechaza_ids_vacios_y_secuencia_negativa()
    {
        var resultado = new MarcarEntregaCommandValidator().Validate(
            new MarcarEntregaCommand(Guid.Empty, Guid.Empty, -1));

        Assert.False(resultado.IsValid);
        Assert.Equal(3, resultado.Errors.Count);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
