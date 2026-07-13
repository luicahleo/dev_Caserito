using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Layering;

public sealed class AislamientoEntreContextosTests
{
    private static readonly string[] _contextos =
        ["Identity", "Catalog", "Chat", "Orders", "Reputation", "Notifications"];

    [Fact]
    public void Ningun_contexto_depende_de_otro_contexto()
    {
        foreach (var ctx in _contextos)
        {
            var otros = _contextos.Where(c => c != ctx).Select(c => $"CaseritoApp.{c}").ToArray();
            foreach (var capa in new[] { "Domain", "Application", "Infrastructure" })
            {
                var ensamblado = Assembly.Load($"CaseritoApp.{ctx}.{capa}");
                var resultado = Types.InAssembly(ensamblado)
                    .Should().NotHaveDependencyOnAny(otros)
                    .GetResult();

                Assert.True(resultado.IsSuccessful,
                    $"{ctx}.{capa} depende de otro contexto: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
            }
        }
    }
}
