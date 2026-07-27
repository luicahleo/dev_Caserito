using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Domain.Notificaciones;
using NSubstitute;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class CrearNotificacionCommandHandlerTests
{
    private readonly INotificacionRepository _repo = Substitute.For<INotificacionRepository>();

    [Fact]
    public async Task Handle_DatosValidos_AgregaNotificacionYRetornaId()
    {
        var handler = new CrearNotificacionCommandHandler(_repo);
        var comando = new CrearNotificacionCommand(
            Guid.NewGuid(),
            TipoNotificacion.NuevoMensaje,
            "Nuevo mensaje",
            "Tienes un nuevo mensaje.",
            Guid.NewGuid());

        var resultado = await handler.Handle(comando, CancellationToken.None);

        Assert.True(resultado.EsExito);
        _repo.Received(1).Agregar(Arg.Any<Notificacion>());
    }
}
