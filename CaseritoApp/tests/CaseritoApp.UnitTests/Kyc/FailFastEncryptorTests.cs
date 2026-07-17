using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class FailFastEncryptorTests
{
    [Fact]
    public void Fuera_de_dev_o_testing_sin_encryptor_real_falla_al_componer()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AgregarIdentity(config, new EntornoStub("Production")));
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
