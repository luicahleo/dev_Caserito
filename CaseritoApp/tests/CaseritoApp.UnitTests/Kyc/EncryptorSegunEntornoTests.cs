using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class EncryptorSegunEntornoTests
{
    [Fact]
    public void Fuera_de_dev_o_testing_registra_encryptor_real()
    {
        var rutaTemporal = Path.Combine(Path.GetTempPath(), $"caserito-keys-{Guid.NewGuid():N}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:RutaClaves"] = rutaTemporal,
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AgregarIdentity(config, new EntornoStub("Production"))
            .BuildServiceProvider();

        Assert.IsType<DataProtectionEncryptor>(provider.GetRequiredService<IEncryptor>());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void En_dev_o_testing_resuelve_passthrough_encryptor(string entorno)
    {
        var config = new ConfigurationBuilder().Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AgregarIdentity(config, new EntornoStub(entorno))
            .BuildServiceProvider();

        Assert.IsType<PassthroughEncryptor>(provider.GetRequiredService<IEncryptor>());
    }

    private sealed class EntornoStub(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
