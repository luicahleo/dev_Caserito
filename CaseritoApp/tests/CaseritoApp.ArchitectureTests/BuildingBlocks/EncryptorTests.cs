using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class EncryptorTests
{
    [Fact]
#pragma warning disable CA1859 // Se prueba deliberadamente a través de la abstracción IEncryptor, no de la implementación concreta.
    public void Passthrough_hace_roundtrip()
    {
        IEncryptor encryptor = new PassthroughEncryptor();
        var original = "12345678";
        Assert.Equal(original, encryptor.Descifrar(encryptor.Cifrar(original)));
    }
#pragma warning restore CA1859
}
