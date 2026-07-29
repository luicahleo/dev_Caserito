using CaseritoApp.Identity.Infrastructure.Correo;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class PlantillaCorreoTextoPlanoTests
{
    private readonly PlantillaCorreoTextoPlano _plantilla = new();

    [Fact]
    public void Confirmacion_email_incluye_url()
    {
        var cuerpo = _plantilla.CuerpoConfirmacionEmail("Luis", "https://caserito.test/confirmar?token=abc");
        Assert.Contains("https://caserito.test/confirmar?token=abc", cuerpo);
    }

    [Fact]
    public void Kyc_rechazado_incluye_motivo()
    {
        var cuerpo = _plantilla.CuerpoKycRechazado("Luis", "El rostro no coincide");
        Assert.Contains("El rostro no coincide", cuerpo);
    }
}
