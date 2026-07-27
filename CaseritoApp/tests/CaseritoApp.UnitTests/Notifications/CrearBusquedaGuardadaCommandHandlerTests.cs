using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Application.Busquedas;
using CaseritoApp.Notifications.Domain.Busquedas;
using NSubstitute;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class CrearBusquedaGuardadaCommandHandlerTests
{
    private readonly IBusquedaGuardadaRepository _repo = Substitute.For<IBusquedaGuardadaRepository>();

    private CrearBusquedaGuardadaCommandHandler CrearHandler() => new(_repo);

    [Fact]
    public async Task Handle_DatosValidos_AgregaBusquedaYRetornaId()
    {
        var comando = new CrearBusquedaGuardadaCommand(
            Guid.NewGuid(),
            "iPhone",
            "Tecnología",
            "Cochabamba",
            1000,
            5000,
            "Usado");

        _repo.AgregarConLimiteAsync(Arg.Any<BusquedaGuardada>(), 20, Arg.Any<CancellationToken>())
            .Returns(Result.Exito());

        var resultado = await CrearHandler().Handle(comando, CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.NotEqual(Guid.Empty, resultado.Valor);
        await _repo.Received(1).AgregarConLimiteAsync(
            Arg.Is<BusquedaGuardada>(b =>
                b.UsuarioId == comando.UsuarioId
                && b.PalabraClave == "iphone"
                && b.Categoria == "tecnología"
                && b.Ciudad == "cochabamba"),
            20,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LimiteAlcanzado_RetornaError()
    {
        var comando = new CrearBusquedaGuardadaCommand(
            Guid.NewGuid(),
            "iPhone",
            null,
            null,
            null,
            null,
            null);

        _repo.AgregarConLimiteAsync(Arg.Any<BusquedaGuardada>(), 20, Arg.Any<CancellationToken>())
            .Returns(Result.Fallo(new Error(
                "limite_busquedas_alcanzado",
                "Se alcanzó el límite de 20 búsquedas guardadas.")));

        var resultado = await CrearHandler().Handle(comando, CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("limite_busquedas_alcanzado", resultado.Error.Code);
    }

    [Fact]
    public async Task Handle_BusquedaInvalida_RetornaErrorDeDominio()
    {
        var comando = new CrearBusquedaGuardadaCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            null);

        var resultado = await CrearHandler().Handle(comando, CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("busqueda_guardada_invalida", resultado.Error.Code);
        await _repo.DidNotReceive().AgregarConLimiteAsync(
            Arg.Any<BusquedaGuardada>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
