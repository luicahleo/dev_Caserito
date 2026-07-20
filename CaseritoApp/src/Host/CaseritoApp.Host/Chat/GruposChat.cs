using System.Globalization;

namespace CaseritoApp.Host.Chat;

public static class GruposChat
{
    public static string ParaConversacion(Guid conversacionId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"chat:conversacion:{conversacionId:N}");
}
