using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class QuitarRolCommandHandlerTests
{
    // Repositorio en memoria: el usuario es AdminPlataforma y es el único (conteo=1).
    private sealed class RepoUltimoAdmin : IRepositorioRolesUsuario
    {
        public Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(string? query, int pagina, int tamano, CancellationToken cancellationToken) =>
            Task.FromResult(new ResultadoPaginado<UsuarioConRolesDto>([], pagina, tamano, 0));
        public Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<int> ContarEnRolAsync(string rol, CancellationToken cancellationToken) => Task.FromResult(1);
        public Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken cancellationToken) => Task.FromResult(Result.Exito());
        public Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken cancellationToken) => Task.FromResult(Result.Exito());
    }

    [Fact]
    public async Task Quitar_ultimo_AdminPlataforma_falla_con_codigo_UltimoAdminPlataforma()
    {
        var handler = new QuitarRolCommandHandler(new RepoUltimoAdmin(), NullLogger<QuitarRolCommandHandler>.Instance);
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.AdminPlataforma);

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(CodigosErrorRoles.UltimoAdminPlataforma, resultado.Error.Code);
    }
}
