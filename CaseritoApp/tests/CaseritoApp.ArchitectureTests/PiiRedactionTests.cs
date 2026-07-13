using CaseritoApp.SmokeLib.Logging;
using Xunit;

namespace CaseritoApp.ArchitectureTests;

public sealed class PiiRedactionTests
{
    [Theory]
    [InlineData("ci")]
    [InlineData("selfie")]
    [InlineData("token")]
    [InlineData("qr")]
    public void Redactar_enmascara_campos_prohibidos(string campo)
    {
        Assert.Equal("***", PiiRedaction.Redactar(campo, "valor-sensible"));
    }

    [Fact]
    public void Redactar_deja_pasar_campos_no_sensibles()
    {
        Assert.Equal("Bicicleta", PiiRedaction.Redactar("titulo", "Bicicleta"));
    }
}
