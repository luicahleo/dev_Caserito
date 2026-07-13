namespace CaseritoApp.BuildingBlocks.Domain;

/// <summary>
/// Representa un error de dominio identificado por un código y un mensaje descriptivo.
/// </summary>
/// <param name="Code">Código único que identifica el tipo de error.</param>
/// <param name="Message">Mensaje descriptivo del error.</param>
// CA1716 (nombre en conflicto con palabra reservada de otro lenguaje, p.ej. "Error" en VB): suprimido
// de forma puntual porque el brief exige explícitamente el nombre de tipo "Error" como parte del contrato público.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Contrato requerido por el diseño: el tipo debe llamarse 'Error' según el brief.")]
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// Instancia que representa la ausencia de error.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
