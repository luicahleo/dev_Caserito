using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Behaviors;
using MediatR;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class UnitOfWorkBehaviorTests
{
    private sealed record Comando : IRequest<string>;

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public int Llamadas { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task Guarda_cambios_una_vez_tras_el_handler()
    {
        var uow = new UnitOfWorkFake();
        var behavior = new UnitOfWorkBehavior<Comando, string>(uow);

        var resultado = await behavior.Handle(new Comando(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", resultado);
        Assert.Equal(1, uow.Llamadas);
    }
}
