using CaseritoApp.BuildingBlocks.Infrastructure.Logging;

namespace CaseritoApp.ArchitectureTests;

public sealed class PiiRedactionTests
{
    [Theory]
    [InlineData("ci")]
    [InlineData("documentoNumero")]
    [InlineData("documentoImagen")]
    [InlineData("selfie")]
    [InlineData("token")]
    [InlineData("qr")]
    [InlineData("pagoReferencia")]
    public void Redactar_enmascara_campos_prohibidos(string campo)
    {
        Assert.Equal("***", PiiRedaction.Redactar(campo, "valor-sensible"));
    }

    [Theory]
    [InlineData("CI")]
    [InlineData("Token")]
    [InlineData("QR")]
    [InlineData("SelFie")]
    public void Redactar_enmascara_campos_prohibidos_sin_distinguir_mayusculas(string campo)
    {
        Assert.Equal("***", PiiRedaction.Redactar(campo, "valor-sensible"));
    }

    [Fact]
    public void Redactar_deja_pasar_campos_no_sensibles()
    {
        Assert.Equal("Bicicleta", PiiRedaction.Redactar("titulo", "Bicicleta"));
    }
}
