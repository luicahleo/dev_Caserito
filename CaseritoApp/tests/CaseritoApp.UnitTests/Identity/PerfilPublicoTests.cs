using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Perfil;

namespace CaseritoApp.UnitTests.Identity;

public sealed class PerfilPublicoTests
{
    [Fact]
    public async Task Devuelve_solo_datos_publicos_cuando_el_usuario_existe()
    {
        var id = Guid.NewGuid();
        var esperado = new PerfilPublicoDto(
            id,
            "Ana Q.",
            Guid.NewGuid(),
            "La Paz",
            true);
        var handler = new ObtenerPerfilPublicoQueryHandler(new RepositorioFake(esperado));

        var resultado = await handler.Handle(new ObtenerPerfilPublicoQuery(id), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(esperado, resultado.Valor);
        Assert.DoesNotContain(
            typeof(PerfilPublicoDto).GetProperties(),
            propiedad => propiedad.Name is "Email" or "Nombres" or "Apellidos" or "Roles" or "Permisos" or "Kyc");
    }

    [Fact]
    public async Task Devuelve_error_generico_cuando_el_usuario_no_existe()
    {
        var handler = new ObtenerPerfilPublicoQueryHandler(new RepositorioFake(null));

        var resultado = await handler.Handle(
            new ObtenerPerfilPublicoQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("reputacion_usuario_no_disponible", resultado.Error.Code);
    }

    private sealed class RepositorioFake(PerfilPublicoDto? perfil) : IRepositorioPerfil
    {
        public Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<PerfilDto?>(null);

        public Task<PerfilPublicoDto?> ObtenerPublicoAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(perfil);

        public Task<Result> ActualizarAsync(
            Guid userId,
            string nombres,
            string apellidos,
            Guid ciudadId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Exito());
    }
}
