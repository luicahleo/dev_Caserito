using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.UnitTests.Chat;

public sealed class IniciarConversacionCommandHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class RepositorioFake : IRepositorioConversaciones
    {
        public Conversacion? Existente { get; set; }

        public List<Conversacion> Agregadas { get; } = [];

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult(Existente);

        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion) => Agregadas.Add(conversacion);
    }

    private sealed class ConsultaAvisoFake(ReferenciaAvisoContactable? referencia) : IConsultaAvisoContactable
    {
        public int Llamadas { get; private set; }

        public Task<ReferenciaAvisoContactable?> ObtenerAsync(Guid avisoId, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(referencia);
        }
    }

    [Fact]
    public async Task Devuelve_existente_sin_consultar_aviso_ni_agregar()
    {
        var compradorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();
        var existente = Conversacion.Crear(avisoId, compradorId, Guid.NewGuid(), _ahora).Valor;
        var repositorio = new RepositorioFake { Existente = existente };
        var consulta = new ConsultaAvisoFake(null);
        var handler = new IniciarConversacionCommandHandler(repositorio, consulta, TimeProvider.System);

        var resultado = await handler.Handle(
            new IniciarConversacionCommand(compradorId, avisoId), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.False(resultado.Valor.FueCreada);
        Assert.Equal(existente.Id, resultado.Valor.Conversacion.Id);
        Assert.Equal(0, consulta.Llamadas);
        Assert.Empty(repositorio.Agregadas);
    }

    [Fact]
    public async Task Aviso_no_contactable_devuelve_no_encontrado()
    {
        var handler = new IniciarConversacionCommandHandler(
            new RepositorioFake(), new ConsultaAvisoFake(null), TimeProvider.System);

        var resultado = await handler.Handle(
            new IniciarConversacionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.AvisoNoContactable, resultado.Error.Code);
    }

    [Fact]
    public async Task Snapshot_de_otro_aviso_se_trata_como_no_contactable()
    {
        var handler = new IniciarConversacionCommandHandler(
            new RepositorioFake(),
            new ConsultaAvisoFake(new ReferenciaAvisoContactable(Guid.NewGuid(), Guid.NewGuid())),
            TimeProvider.System);

        var resultado = await handler.Handle(
            new IniciarConversacionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.AvisoNoContactable, resultado.Error.Code);
    }

    [Fact]
    public async Task Autochat_devuelve_conflicto()
    {
        var usuarioId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();
        var handler = new IniciarConversacionCommandHandler(
            new RepositorioFake(),
            new ConsultaAvisoFake(new ReferenciaAvisoContactable(avisoId, usuarioId)),
            TimeProvider.System);

        var resultado = await handler.Handle(
            new IniciarConversacionCommand(usuarioId, avisoId), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.ParticipantesCoinciden, resultado.Error.Code);
    }

    [Fact]
    public async Task Crea_con_reloj_de_servidor_y_agrega_una_vez()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();
        var repositorio = new RepositorioFake();
        var reloj = new RelojFijo(_ahora);
        var handler = new IniciarConversacionCommandHandler(
            repositorio,
            new ConsultaAvisoFake(new ReferenciaAvisoContactable(avisoId, vendedorId)),
            reloj);

        var resultado = await handler.Handle(
            new IniciarConversacionCommand(compradorId, avisoId), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.True(resultado.Valor.FueCreada);
        Assert.Equal(_ahora, resultado.Valor.Conversacion.CreadaEn);
        Assert.Equal(vendedorId, resultado.Valor.Conversacion.VendedorId);
        Assert.Single(repositorio.Agregadas);
    }

    [Fact]
    public void Validator_rechaza_identificadores_vacios()
    {
        var validator = new IniciarConversacionCommandValidator();

        var resultado = validator.Validate(new IniciarConversacionCommand(Guid.Empty, Guid.Empty));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(IniciarConversacionCommand.CompradorId));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(IniciarConversacionCommand.AvisoId));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
