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

    /// <summary>
    /// Los mismos contextos en el formato que consume <c>[MemberData]</c>, para
    /// las suites que ejecutan una regla por bounded context.
    /// </summary>
    /// <remarks>
    /// Se declara <c>public</c> —y no <c>internal</c> como el resto de la clase—
    /// porque el analizador xUnit1016 exige que el miembro referenciado por
    /// <c>[MemberData]</c> sea público. Al estar la clase contenedora marcada
    /// como <c>internal</c>, la visibilidad efectiva sigue siendo el ensamblado.
    /// </remarks>
    public static TheoryData<string> ContextosComoDatos()
    {
        var datos = new TheoryData<string>();
        foreach (var contexto in Contextos)
        {
            datos.Add(contexto);
        }
        return datos;
    }

    internal static Assembly Cargar(string nombre) => Assembly.Load(nombre);

    internal static Assembly De(string contexto, string capa) =>
        Cargar($"CaseritoApp.{contexto}.{capa}");
}
