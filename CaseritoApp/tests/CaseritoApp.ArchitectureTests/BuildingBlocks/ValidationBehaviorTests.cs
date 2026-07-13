using CaseritoApp.BuildingBlocks.Application.Behaviors;
using FluentValidation;
using MediatR;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class ValidationBehaviorTests
{
    private sealed record Comando(string Nombre) : IRequest<string>;

    private sealed class ComandoValidator : AbstractValidator<Comando>
    {
        public ComandoValidator() => RuleFor(c => c.Nombre).NotEmpty();
    }

    [Fact]
    public async Task Lanza_validation_exception_cuando_es_invalido()
    {
        var behavior = new ValidationBehavior<Comando, string>([new ComandoValidator()]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new Comando(""), () => Task.FromResult("ok"), CancellationToken.None));
    }

    [Fact]
    public async Task Continua_cuando_es_valido()
    {
        var behavior = new ValidationBehavior<Comando, string>([new ComandoValidator()]);

        var resultado = await behavior.Handle(new Comando("x"), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", resultado);
    }
}
