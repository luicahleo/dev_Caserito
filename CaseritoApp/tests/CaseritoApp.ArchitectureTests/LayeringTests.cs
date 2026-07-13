using CaseritoApp.SmokeLib.Domain;
using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

public sealed class LayeringTests
{
    private const string DomainNamespace = "CaseritoApp.SmokeLib.Domain";
    private const string ApplicationNamespace = "CaseritoApp.SmokeLib.Application";

    [Fact]
    public void Domain_no_debe_depender_de_Application()
    {
        var resultado = Types.InAssembly(typeof(SampleEntity).Assembly)
            .That().ResideInNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Tipos que violan la regla: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }
}
