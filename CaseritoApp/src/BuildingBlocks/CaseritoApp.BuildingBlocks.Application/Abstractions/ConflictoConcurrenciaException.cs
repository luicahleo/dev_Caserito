namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Señala que una operación perdió una carrera de concurrencia optimista al persistir (otra
/// transacción modificó el mismo agregado). Es neutral respecto a la persistencia (no expone tipos
/// de EF Core); la capa de presentación la mapea a HTTP 409.
/// </summary>
public sealed class ConflictoConcurrenciaException : Exception
{
    public ConflictoConcurrenciaException()
    {
    }

    public ConflictoConcurrenciaException(string message) : base(message)
    {
    }

    public ConflictoConcurrenciaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
