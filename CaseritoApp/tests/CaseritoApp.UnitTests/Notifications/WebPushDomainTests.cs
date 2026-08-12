using CaseritoApp.Notifications.Domain.Push;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class WebPushDomainTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Suscripcion_se_actualiza_idempotentemente_y_solo_el_propietario_revoca()
    {
        var usuarioId = Guid.NewGuid();
        var resultado = SuscripcionPush.Crear(
            usuarioId, "dispositivo", "https://push.example.test/a", "clave-publica", "secreto-auth", _ahora);

        Assert.True(resultado.EsExito);
        var suscripcion = resultado.Valor;
        Assert.True(suscripcion.Actualizar(
            usuarioId, "https://push.example.test/b", "otra-clave", "otro-auth", _ahora.AddMinutes(1)).EsExito);
        Assert.False(suscripcion.Revocar(Guid.NewGuid(), _ahora).EsExito);
        Assert.True(suscripcion.Revocar(usuarioId, _ahora.AddMinutes(2)).EsExito);
        Assert.False(suscripcion.Activa);
    }

    [Theory]
    [InlineData("http://push.example.test/a", "clave", "auth")]
    [InlineData("https://push.example.test/a", "", "auth")]
    [InlineData("https://push.example.test/a", "clave", "")]
    public void Suscripcion_rechaza_datos_invalidos_sin_reflejarlos(
        string endpoint, string p256dh, string auth)
    {
        var resultado = SuscripcionPush.Crear(
            Guid.NewGuid(), "dispositivo", endpoint, p256dh, auth, _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal("La suscripción no es válida.", resultado.Error.Message);
        Assert.DoesNotContain(endpoint, resultado.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Intencion_aplica_lease_reintento_y_finalizacion_monotonicos()
    {
        var intencion = IntencionPush.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, _ahora).Valor;

        Assert.True(intencion.Reclamar(_ahora, TimeSpan.FromMinutes(1)));
        Assert.False(intencion.Reclamar(_ahora.AddSeconds(30), TimeSpan.FromMinutes(1)));
        intencion.Reprogramar(_ahora.AddMinutes(1), TimeSpan.FromMinutes(2));
        Assert.Equal(1, intencion.Intentos);
        Assert.True(intencion.Reclamar(_ahora.AddMinutes(3), TimeSpan.FromMinutes(1)));
        intencion.MarcarProcesada(_ahora.AddMinutes(3));
        Assert.True(intencion.Procesada);
        Assert.False(intencion.Reclamar(_ahora.AddMinutes(4), TimeSpan.FromMinutes(1)));
    }
}
