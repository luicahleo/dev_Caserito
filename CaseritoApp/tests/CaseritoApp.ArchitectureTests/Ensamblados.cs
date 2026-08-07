using System.Reflection;

namespace CaseritoApp.ArchitectureTests;

/// <summary>
/// Resuelve los ensamblados de la solución por nombre. El proyecto de tests
/// referencia todos los csproj, así que los DLL están en el directorio de salida.
/// </summary>
internal static class Ensamblados
{
    /// <summary>Bounded contexts con las tres capas completas.</summary>
    internal static readonly string[] Contextos =
    [
        "Identity",
        "Catalog",
        "Chat",
        "Orders",
        "Reputation",
        "Notifications",
    ];

    internal static Assembly Cargar(string nombre) => Assembly.Load(nombre);

    internal static Assembly De(string contexto, string capa) =>
        Cargar($"CaseritoApp.{contexto}.{capa}");
}
