namespace CaseritoApp.Chat.Infrastructure;

public sealed class ConflictoUnicidadChatException : Exception
{
    public ConflictoUnicidadChatException()
    {
    }

    public ConflictoUnicidadChatException(string message) : base(message)
    {
    }

    public ConflictoUnicidadChatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
