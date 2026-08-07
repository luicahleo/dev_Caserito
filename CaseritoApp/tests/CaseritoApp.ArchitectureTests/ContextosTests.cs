using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Los bounded contexts se mantienen aislados: ninguno conoce el Domain, el
/// Application ni el Infrastructure de otro. La comunicación entre contextos
/// ocurre a través de BuildingBlocks.Contracts.
/// Absorbe la regla del antiguo
/// <c>Layering/AislamientoEntreContextosTests.Ningun_contexto_depende_de_otro_contexto</c>,
/// con un caso por par de contextos para diagnosticar mejor qué par falla.
/// </summary>
public sealed class ContextosTests
{
    public static TheoryData<string, string> ParesDeContextos()
    {
        var datos = new TheoryData<string, string>();
        foreach (var origen in Ensamblados.Contextos)
        {
            foreach (var destino in Ensamblados.Contextos)
            {
                if (origen != destino)
                {
                    datos.Add(origen, destino);
                }
            }
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(ParesDeContextos))]
    public void Domain_no_conoce_otros_contextos(string origen, string destino)
    {
        var resultado = Types.InAssembly(Ensamblados.De(origen, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{destino}.Domain",
                $"CaseritoApp.{destino}.Application",
                $"CaseritoApp.{destino}.Infrastructure")
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"{origen}.Domain depende de {destino}. Tipos: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    /// <summary>
    /// El Application de un contexto tampoco puede conocer el Application ajeno:
    /// la capa pública de un contexto es BuildingBlocks.Contracts, no su Application.
    /// </summary>
    [Theory]
    [MemberData(nameof(ParesDeContextos))]
    public void Application_no_conoce_el_interior_de_otros_contextos(string origen, string destino)
    {
        var resultado = Types.InAssembly(Ensamblados.De(origen, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{destino}.Domain",
                $"CaseritoApp.{destino}.Application",
                $"CaseritoApp.{destino}.Infrastructure")
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"{origen}.Application depende del interior de {destino}. " +
            $"Usa BuildingBlocks.Contracts. Tipos: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    /// <summary>
    /// El Infrastructure de un contexto también queda aislado: la persistencia y
    /// los adaptadores de un contexto no pueden alcanzar a otro contexto.
    /// </summary>
    [Theory]
    [MemberData(nameof(ParesDeContextos))]
    public void Infrastructure_no_conoce_el_interior_de_otros_contextos(string origen, string destino)
    {
        var resultado = Types.InAssembly(Ensamblados.De(origen, "Infrastructure"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{destino}.Domain",
                $"CaseritoApp.{destino}.Application",
                $"CaseritoApp.{destino}.Infrastructure")
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"{origen}.Infrastructure depende del interior de {destino}. " +
            $"Usa BuildingBlocks.Contracts. Tipos: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
