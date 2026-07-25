using CaseritoApp.Host.Endpoints;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;
using CaseritoApp.Reputation.Infrastructure;
using NetArchTest.Rules;

namespace CaseritoApp.ArchitectureTests.Reputation;

public sealed class ReputationArchitectureTests
{
    [Fact]
    public void Capas_de_reputation_respetan_dependencias_permitidas()
    {
        var domain = typeof(Resena).Assembly;
        var application = typeof(CrearResenaCommand).Assembly;
        var infrastructure = typeof(ReputationDbContext).Assembly;

        Assert.DoesNotContain(
            domain.GetReferencedAssemblies(),
            referencia => referencia.Name!.StartsWith("CaseritoApp.", StringComparison.Ordinal)
                && referencia.Name != "CaseritoApp.BuildingBlocks.Domain");
        Assert.True(Types.InAssembly(application)
            .Should()
            .NotHaveDependencyOnAny(
                "CaseritoApp.Reputation.Infrastructure",
                "CaseritoApp.Orders",
                "CaseritoApp.Identity",
                "CaseritoApp.Catalog")
            .GetResult()
            .IsSuccessful);
        Assert.True(Types.InAssembly(infrastructure)
            .Should()
            .NotHaveDependencyOnAny(
                "CaseritoApp.Orders",
                "CaseritoApp.Identity",
                "CaseritoApp.Catalog")
            .GetResult()
            .IsSuccessful);
    }

    [Fact]
    public void Dtos_publicos_no_exponen_identificadores_internos_ni_pii()
    {
        Type[] tipos =
        [
            typeof(PerfilPublicoConReputacionDto),
            typeof(ResumenReputacionDto),
            typeof(ResenaPublicaDto),
            typeof(ResultadoPaginadoResenasDto),
        ];
        string[] prohibidos =
        [
            "Email",
            "Role",
            "Permiso",
            "Kyc",
            "OrderId",
            "AutorId",
            "AuthorId",
            "DestinatarioId",
            "RecipientId",
        ];

        Assert.All(tipos, tipo => Assert.DoesNotContain(
            tipo.GetProperties(),
            propiedad => prohibidos.Any(nombre =>
                propiedad.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase))));
    }
}
