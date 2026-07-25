namespace CaseritoApp.Reputation.Infrastructure;

public sealed class ConflictoUnicidadReputationException : Exception
{
    public ConflictoUnicidadReputationException()
    {
    }

    public ConflictoUnicidadReputationException(string message)
        : base(message)
    {
    }

    public ConflictoUnicidadReputationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
