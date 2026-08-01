using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Auth;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class RestablecerPasswordHandlerTests
{
    private sealed class RepositorioFake(Result<Guid> resultado) : IRepositorioRestablecimientoPassword
    {
        public Task<SolicitudRestablecimiento?> CrearSolicitudAsync(
            string email,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<Guid>> RestablecerAsync(
            Guid usuarioId,
            string token,
            string password,
            CancellationToken cancellationToken) => Task.FromResult(resultado);
    }

    private sealed class RevocadorFake : IRevocadorSesionesUsuario
    {
        public int Llamadas { get; private set; }
        public Guid UsuarioId { get; private set; }

        public Task RevocarTodasAsync(Guid usuarioId, CancellationToken cancellationToken)
        {
            Llamadas++;
            UsuarioId = usuarioId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Exito_revoca_todas_las_sesiones_del_usuario()
    {
        var usuarioId = Guid.NewGuid();
        var revocador = new RevocadorFake();
        var handler = new RestablecerPasswordCommandHandler(
            new RepositorioFake(Result.Exito(usuarioId)),
            revocador);

        var resultado = await handler.Handle(
            new RestablecerPasswordCommand(usuarioId, "token", "Password123!"),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(1, revocador.Llamadas);
        Assert.Equal(usuarioId, revocador.UsuarioId);
    }

    [Fact]
    public async Task Fallo_no_revoca_sesiones_y_conserva_error_generico()
    {
        var error = new Error(
            "Auth.EnlaceRestablecimientoInvalido",
            "El enlace no es válido o ha caducado.");
        var revocador = new RevocadorFake();
        var handler = new RestablecerPasswordCommandHandler(
            new RepositorioFake(Result.Fallo<Guid>(error)),
            revocador);

        var resultado = await handler.Handle(
            new RestablecerPasswordCommand(Guid.NewGuid(), "token-sensible", "Password123!"),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(error, resultado.Error);
        Assert.Equal(0, revocador.Llamadas);
        Assert.DoesNotContain("token-sensible", resultado.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Password123!", resultado.Error.Message, StringComparison.Ordinal);
    }
}
