using Microsoft.Net.Http.Headers;

namespace CaseritoApp.Host;

public static class PwaCachePolicy
{
    private static readonly PathString[] _archivosDeArranque =
    [
        new("/index.html"),
        new("/sw.js"),
        new("/registerSW.js"),
        new("/manifest.webmanifest"),
    ];

    public static bool RequiereRevalidacion(PathString ruta, string? tipoContenido) =>
        tipoContenido?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) is true
        || _archivosDeArranque.Contains(ruta);

    public static void Aplicar(HttpResponse respuesta)
    {
        respuesta.Headers[HeaderNames.CacheControl] = "no-cache, must-revalidate";
        respuesta.Headers[HeaderNames.Pragma] = "no-cache";
        respuesta.Headers[HeaderNames.Expires] = "0";
    }
}
