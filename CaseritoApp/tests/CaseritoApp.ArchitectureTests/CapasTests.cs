using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Reglas de dependencia de Clean Architecture. Las capas internas no pueden
/// conocer a las externas ni a la infraestructura técnica.
/// </summary>
public sealed class CapasTests
{
    /// <summary>
    /// El dominio no puede conocer a Application, Infrastructure ni al Host.
    /// Absorbe la regla del antiguo
    /// <c>Layering/CapasPorContextoTests.Domain_no_depende_de_Application_ni_Infrastructure</c>,
    /// añadiendo <c>CaseritoApp.Host</c> y un caso por contexto para diagnosticar mejor.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ensamblados.ContextosComoDatos), MemberType = typeof(Ensamblados))]
    public void Domain_no_depende_de_capas_externas(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{contexto}.Application",
                $"CaseritoApp.{contexto}.Infrastructure",
                "CaseritoApp.Host")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Domain"));
    }

    /// <summary>
    /// El dominio no puede conocer la infraestructura técnica: ni persistencia
    /// (EF Core) ni el stack web (ASP.NET Core).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>MediatR está deliberadamente fuera de la lista de dependencias
    /// prohibidas.</b> No es un descuido: es una excepción decidida y
    /// documentada. El marcador de eventos de dominio
    /// <c>CaseritoApp.BuildingBlocks.Domain.IDomainEvent</c> hereda de
    /// <c>MediatR.INotification</c> por diseño, y <c>CaseritoApp/AGENTS.md</c>
    /// declara MediatR herramienta transversal del proyecto (CQRS-lite). Cada
    /// evento de dominio de Identity, Catalog, Chat y Orders arrastra esa
    /// referencia a través de <c>IDomainEvent</c>. Prohibir MediatR aquí
    /// obligaría a refactorizar el despachador de eventos sin beneficio real.
    /// No añadir MediatR a la lista sin revisar antes esa decisión.
    /// </para>
    /// <para>
    /// Reputation y Notifications pasarían esta regla incluso con MediatR
    /// prohibido, pero solo porque hoy no tienen ningún evento de dominio: ese
    /// verde sería accidental, no una señal de que estén mejor aisladas.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Ensamblados.ContextosComoDatos), MemberType = typeof(Ensamblados))]
    public void Domain_no_depende_de_infraestructura_tecnica(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Domain"));
    }

    [Theory]
    [MemberData(nameof(Ensamblados.ContextosComoDatos), MemberType = typeof(Ensamblados))]
    public void Application_no_depende_de_Infrastructure_ni_de_EF(string contexto)
    {
        var resultado = Types.InAssembly(Ensamblados.De(contexto, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny(
                $"CaseritoApp.{contexto}.Infrastructure",
                "CaseritoApp.Host",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Describir(resultado, $"{contexto}.Application"));
    }

    private static string Describir(TestResult resultado, string origen)
    {
        var tipos = resultado.FailingTypeNames ?? [];
        return $"{origen} viola la regla de capas. Tipos infractores: {string.Join(", ", tipos)}";
    }
}
