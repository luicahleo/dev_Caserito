using CaseritoApp.Identity.Infrastructure.Correo;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class OpcionesCorreoTests
{
    [Fact]
    public void Valores_por_defecto_son_los_esperados()
    {
        var opciones = new OpcionesCorreo();

        Assert.Equal("mail", opciones.Host);
        Assert.Equal(587, opciones.Puerto);
        Assert.Equal("noreply@trajano.online", opciones.Remitente);
        Assert.Equal("Caserito", opciones.NombreRemitente);
        Assert.False(opciones.HabilitarSsl);
    }
}
