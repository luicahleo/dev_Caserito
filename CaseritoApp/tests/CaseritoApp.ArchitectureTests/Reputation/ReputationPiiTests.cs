using CaseritoApp.Host.Endpoints;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;
using CaseritoApp.Reputation.Infrastructure;

namespace CaseritoApp.ArchitectureTests.Reputation;

public sealed class ReputationPiiTests
{
    [Fact]
    public void Componentes_de_reputation_no_inyectan_logging()
    {
        var tipos = new[]
        {
            typeof(Resena),
            typeof(CrearResenaCommandHandler),
            typeof(ReputationDbContext),
            typeof(ReputationEndpoints),
            typeof(PerfilesPublicosEndpoints),
        };

        Assert.All(tipos, tipo => Assert.DoesNotContain(
            tipo.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parametro => parametro.ParameterType.FullName?.Contains(
                "ILogger",
                StringComparison.Ordinal) == true));
    }

    [Fact]
    public void Fuentes_de_reputation_no_contienen_logging_ni_mojibake()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "CaseritoApp.sln")))
        {
            raiz = raiz.Parent;
        }

        Assert.NotNull(raiz);
        var archivos = Directory.GetFiles(
            Path.Combine(raiz.FullName, "src", "Reputation"),
            "*.cs",
            SearchOption.AllDirectories)
            .Append(Path.Combine(
                raiz.FullName,
                "src",
                "Host",
                "CaseritoApp.Host",
                "Endpoints",
                "ReputationEndpoints.cs"))
            .Append(Path.Combine(
                raiz.FullName,
                "src",
                "Host",
                "CaseritoApp.Host",
                "Endpoints",
                "PerfilesPublicosEndpoints.cs"));

        Assert.All(archivos, archivo =>
        {
            var contenido = File.ReadAllText(archivo);
            Assert.DoesNotContain("ILogger", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("LogInformation", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("LogWarning", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("LogError", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("Ãƒ", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("Ã¢", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("ï¿½", contenido, StringComparison.Ordinal);
        });
    }
}
