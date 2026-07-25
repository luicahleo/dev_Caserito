using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.UnitTests.Reputation;

public sealed class CrearResenaCommandHandlerTests
{
    [Fact]
    public async Task Crea_resena_con_participantes_derivados_de_la_orden()
    {
        var actorId = Guid.NewGuid();
        var contraparteId = Guid.NewGuid();
        var repositorio = new RepositorioFalso();
        var handler = new CrearResenaCommandHandler(
            repositorio,
            new OrdenFalsa(new OrdenCalificable(
                Guid.NewGuid(),
                actorId,
                contraparteId,
                RolAutorResena.Comprador)));
        var orderId = Guid.NewGuid();

        var resultado = await handler.Handle(
            new CrearResenaCommand(orderId, actorId, 5, "Cumplió con todo lo acordado."),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        var resena = Assert.Single(repositorio.Agregadas);
        Assert.Equal(orderId, resena.OrderId);
        Assert.Equal(actorId, resena.AutorId);
        Assert.Equal(contraparteId, resena.DestinatarioId);
        Assert.Equal(RolAutorResena.Comprador, resena.RolAutor);
    }

    [Fact]
    public async Task Orden_no_disponible_devuelve_error_generico()
    {
        var handler = new CrearResenaCommandHandler(
            new RepositorioFalso(),
            new OrdenFalsa(null));

        var resultado = await handler.Handle(
            new CrearResenaCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                4,
                "Comentario completamente válido."),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.OrdenNoDisponible, resultado.Error.Code);
    }

    [Fact]
    public async Task Autor_duplicado_no_agrega_otra_resena()
    {
        var actorId = Guid.NewGuid();
        var repositorio = new RepositorioFalso { Existe = true };
        var handler = new CrearResenaCommandHandler(
            repositorio,
            new OrdenFalsa(new OrdenCalificable(
                Guid.NewGuid(),
                actorId,
                Guid.NewGuid(),
                RolAutorResena.Vendedor)));

        var resultado = await handler.Handle(
            new CrearResenaCommand(
                Guid.NewGuid(),
                actorId,
                4,
                "Comentario completamente válido."),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Duplicada, resultado.Error.Code);
        Assert.Empty(repositorio.Agregadas);
    }

    [Theory]
    [InlineData(0, "Comentario válido.")]
    [InlineData(6, "Comentario válido.")]
    [InlineData(5, "corto")]
    public void Validador_rechaza_entrada_invalida(int puntuacion, string comentario)
    {
        var command = new CrearResenaCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            puntuacion,
            comentario);

        var resultado = new CrearResenaCommandValidator().Validate(command);

        Assert.False(resultado.IsValid);
    }

    private sealed class OrdenFalsa(OrdenCalificable? orden) : IConsultaOrdenCalificable
    {
        public Task<OrdenCalificable?> ObtenerAsync(
            Guid orderId,
            Guid actorId,
            CancellationToken ct) =>
            Task.FromResult(orden is null ? null : orden with { OrderId = orderId });
    }

    private sealed class RepositorioFalso : IRepositorioResenas
    {
        public bool Existe { get; init; }

        public List<Resena> Agregadas { get; } = [];

        public Task<bool> ExisteAsync(Guid orderId, Guid autorId, CancellationToken ct) =>
            Task.FromResult(Existe);

        public void Agregar(Resena resena) => Agregadas.Add(resena);
    }
}
