using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class ServicioCorreoSmtpTests
{
    [Fact]
    public void Construye_con_opciones_por_defecto()
    {
        var opciones = Options.Create(new OpcionesCorreo());
        var servicio = new ServicioCorreoSmtp(opciones, NullLogger<ServicioCorreoSmtp>.Instance);
        Assert.NotNull(servicio);
    }
}
