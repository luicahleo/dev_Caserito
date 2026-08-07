using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Convenciones de CQRS-lite verificadas de forma automática. Codifican la
/// convención real del repositorio: los handlers y los validadores se declaran
/// como <c>public sealed class …Handler</c> / <c>…Validator</c> y viven en la
/// capa Application de su bounded context, nunca en Infrastructure.
/// El alcance es la capa Application e Infrastructure de cada contexto; el Host
/// y los BuildingBlocks quedan fuera porque alojan handlers de infraestructura
/// técnica (por ejemplo el manejador global de excepciones de ASP.NET Core).
/// </summary>
public sealed class ConvencionesTests
{
    public static TheoryData<string> ContextosDeNegocio()
    {
        var datos = new TheoryData<string>();
        foreach (var contexto in Ensamblados.Contextos)
        {
            datos.Add(contexto);
        }
        return datos;
    }

    /// <summary>
    /// Un handler es una unidad de comportamiento sin herencia prevista: sellarlo
    /// evita jerarquías accidentales y permite al runtime desvirtualizar llamadas.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContextosDeNegocio))]
    public void Los_handlers_son_sealed(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Handlers no sellados en {contexto}.Application: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    /// <summary>
    /// La orquestación de casos de uso pertenece a Application. Un handler CQRS
    /// en Infrastructure invertiría la dependencia y saltaría la validación y el
    /// pipeline de MediatR configurados en Application.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContextosDeNegocio))]
    public void Los_handlers_viven_en_la_capa_Application(string contexto)
    {
        var enInfraestructura = Types.InAssembly(Ensamblados.De(contexto, "Infrastructure"))
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Or()
            .HaveNameEndingWith("QueryHandler")
            .GetTypes()
            .ToList();

        Assert.True(
            enInfraestructura.Count == 0,
            $"Handlers CQRS fuera de Application en {contexto}.Infrastructure: " +
            string.Join(", ", enInfraestructura.Select(tipo => tipo.Name)));
    }

    /// <summary>
    /// Los validadores de FluentValidation se resuelven por convención desde el
    /// ensamblado; sellarlos mantiene una regla de validación por caso de uso y
    /// evita que una subclase altere en silencio las reglas heredadas.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContextosDeNegocio))]
    public void Los_validadores_son_sealed(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            resultado.IsSuccessful,
            $"Validadores no sellados en {contexto}.Application: " +
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
