using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class DataProtectionEncryptorTests
{
    private static DataProtectionEncryptor CrearEncryptor(IDataProtectionProvider? provider = null)
        => new(provider ?? DataProtectionProvider.Create("CaseritoTest"));

    [Fact]
    public void Cifrar_y_descifrar_bytes_round_trip()
    {
        var encryptor = CrearEncryptor();
        var datos = new byte[] { 1, 2, 3, 250, 0, 17 };

        var cifrado = encryptor.Cifrar(datos);

        Assert.NotEqual(datos, cifrado);
        Assert.Equal(datos, encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Cifrar_y_descifrar_texto_round_trip_utf8()
    {
        var encryptor = CrearEncryptor();

        var cifrado = encryptor.Cifrar("CI 1234567 — José Ñañez");

        Assert.NotEqual("CI 1234567 — José Ñañez", cifrado);
        Assert.Equal("CI 1234567 — José Ñañez", encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Descifrar_payload_alterado_falla()
    {
        var encryptor = CrearEncryptor();
        var cifrado = encryptor.Cifrar(new byte[] { 9, 9, 9 });
        cifrado[0] ^= 0xFF;

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Otro_proposito_no_descifra()
    {
        var provider = DataProtectionProvider.Create("CaseritoTest");
        var cifrado = CrearEncryptor(provider).Cifrar("secreto");
        var otro = provider.CreateProtector("Otro.Proposito");

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => otro.Unprotect(System.Buffers.Text.Base64Url.DecodeFromChars(cifrado)));
    }
}
