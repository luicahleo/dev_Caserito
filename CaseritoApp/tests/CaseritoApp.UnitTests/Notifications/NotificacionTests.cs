using CaseritoApp.Notifications.Domain.Notificaciones;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class NotificacionTests
{
    [Fact]
    public void Crear_ConDatosValidos_RetornaExito()
    {
        var resultado = Notificacion.Crear(
            Guid.NewGuid(),
            TipoNotificacion.NuevoMensaje,
            "Nuevo mensaje",
            "Tienes un nuevo mensaje de chat.",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.True(resultado.EsExito);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_TituloInvalido_RetornaFallo(string? titulo)
    {
        var resultado = Notificacion.Crear(
            Guid.NewGuid(),
            TipoNotificacion.NuevoMensaje,
            titulo!,
            "Mensaje válido de más de diez caracteres.",
            null,
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
    }
}
