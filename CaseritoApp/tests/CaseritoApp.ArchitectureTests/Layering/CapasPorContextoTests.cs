using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Layering;

public sealed class CapasPorContextoTests
{
    private static readonly string[] _contextos =
        ["Identity", "Catalog", "Chat", "Orders", "Reputation", "Notifications"];

    [Fact]
    public void Domain_no_depende_de_Application_ni_Infrastructure()
    {
        foreach (var ctx in _contextos)
        {
            var domain = Assembly.Load($"CaseritoApp.{ctx}.Domain");
            var resultado = Types.InAssembly(domain)
                .Should().NotHaveDependencyOnAny(
                    $"CaseritoApp.{ctx}.Application",
                    $"CaseritoApp.{ctx}.Infrastructure")
                .GetResult();

            Assert.True(resultado.IsSuccessful,
                $"{ctx}.Domain viola capas: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
        }
    }
}
