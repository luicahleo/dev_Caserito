namespace CaseritoApp.Orders.Infrastructure;

public sealed class ConflictoUnicidadOrdersException : Exception
{
    public ConflictoUnicidadOrdersException()
    {
    }

    public ConflictoUnicidadOrdersException(string message) : base(message)
    {
    }

    public ConflictoUnicidadOrdersException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
