using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using NSubstitute;

namespace CaseritoApp.UnitTests.Auth;

public sealed class ConfirmarEmailCommandHandlerTests
{
    [Fact]
    public async Task Token_valido_confirma_email_y_devuelve_exito()
    {
        var userId = Guid.NewGuid();
        var generador = Substitute.For<IGeneradorTokenEmail>();
        generador.Validar("token-valido", out Arg.Any<Guid>())
            .Returns(x =>
            {
                x[1] = userId;
                return true;
            });

        var repositorio = Substitute.For<IRepositorioConfirmacionEmail>();
        repositorio.ConfirmarEmailAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Exito());

        var handler = new ConfirmarEmailCommandHandler(repositorio, generador);
        var resultado = await handler.Handle(
            new ConfirmarEmailCommand(userId, "token-valido"),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        await repositorio.Received(1).ConfirmarEmailAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_invalido_devuelve_fallo_sin_llamar_repositorio()
    {
        var generador = Substitute.For<IGeneradorTokenEmail>();
        generador.Validar("token-invalido", out Arg.Any<Guid>()).Returns(false);

        var repositorio = Substitute.For<IRepositorioConfirmacionEmail>();
        var handler = new ConfirmarEmailCommandHandler(repositorio, generador);

        var resultado = await handler.Handle(
            new ConfirmarEmailCommand(Guid.NewGuid(), "token-invalido"),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("Auth.TokenConfirmacionInvalido", resultado.Error.Code);
        await repositorio.DidNotReceiveWithAnyArgs()
            .ConfirmarEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_valido_para_otro_usuario_devuelve_fallo()
    {
        var generador = Substitute.For<IGeneradorTokenEmail>();
        generador.Validar("token-otro", out Arg.Any<Guid>())
            .Returns(x =>
            {
                x[1] = Guid.NewGuid();
                return true;
            });

        var repositorio = Substitute.For<IRepositorioConfirmacionEmail>();
        var handler = new ConfirmarEmailCommandHandler(repositorio, generador);

        var resultado = await handler.Handle(
            new ConfirmarEmailCommand(Guid.NewGuid(), "token-otro"),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("Auth.TokenConfirmacionInvalido", resultado.Error.Code);
        await repositorio.DidNotReceiveWithAnyArgs()
            .ConfirmarEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Usuario_no_encontrado_devuelve_fallo_del_repositorio()
    {
        var userId = Guid.NewGuid();
        var generador = Substitute.For<IGeneradorTokenEmail>();
        generador.Validar("token-valido", out Arg.Any<Guid>())
            .Returns(x =>
            {
                x[1] = userId;
                return true;
            });

        var repositorio = Substitute.For<IRepositorioConfirmacionEmail>();
        repositorio.ConfirmarEmailAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fallo(new Error("Auth.UsuarioNoEncontrado", "El usuario no existe.")));

        var handler = new ConfirmarEmailCommandHandler(repositorio, generador);
        var resultado = await handler.Handle(
            new ConfirmarEmailCommand(userId, "token-valido"),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("Auth.UsuarioNoEncontrado", resultado.Error.Code);
    }
}
