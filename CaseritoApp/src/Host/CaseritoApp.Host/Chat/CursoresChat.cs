using System.Buffers.Binary;
using CaseritoApp.Chat.Application.Conversaciones;
using Microsoft.AspNetCore.WebUtilities;

namespace CaseritoApp.Host.Chat;

public static class CursoresChat
{
    private const byte Version = 1;

    public static string Codificar(FronteraConversaciones frontera)
    {
        var datos = new byte[25];
        datos[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(datos.AsSpan(1, 8), frontera.UltimaActividadEn.UtcTicks);
        frontera.ConversacionId.TryWriteBytes(datos.AsSpan(9, 16));
        return WebEncoders.Base64UrlEncode(datos);
    }

    public static string Codificar(long secuencia)
    {
        var datos = new byte[9];
        datos[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(datos.AsSpan(1, 8), secuencia);
        return WebEncoders.Base64UrlEncode(datos);
    }

    public static bool TryDecodificarConversaciones(
        string? cursor,
        out FronteraConversaciones frontera)
    {
        frontera = default;
        if (!TryDecodificar(cursor, 25, out var datos))
        {
            return false;
        }

        try
        {
            var ticks = BinaryPrimitives.ReadInt64BigEndian(datos.AsSpan(1, 8));
            var id = new Guid(datos.AsSpan(9, 16));
            if (id == Guid.Empty)
            {
                return false;
            }

            frontera = new FronteraConversaciones(
                new DateTimeOffset(ticks, TimeSpan.Zero),
                id);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static bool TryDecodificarMensajes(string? cursor, out long secuencia)
    {
        secuencia = 0;
        if (!TryDecodificar(cursor, 9, out var datos))
        {
            return false;
        }

        secuencia = BinaryPrimitives.ReadInt64BigEndian(datos.AsSpan(1, 8));
        return secuencia > 0;
    }

    private static bool TryDecodificar(string? cursor, int longitud, out byte[] datos)
    {
        datos = [];
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            datos = WebEncoders.Base64UrlDecode(cursor);
            return datos.Length == longitud && datos[0] == Version;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
