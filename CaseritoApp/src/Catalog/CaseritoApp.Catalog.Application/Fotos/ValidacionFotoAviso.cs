namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>
/// Reglas de aceptaci&#xf3;n de fotos de aviso: whitelist de tipo MIME, l&#xed;mite de tama&#xf1;o
/// y verificaci&#xf3;n de magic bytes (no se conf&#xed;a en la extensi&#xf3;n ni en el content-type declarado).
/// </summary>
public static class ValidacionFotoAviso
{
    /// <summary>Límite absoluto de entrada por foto (25 MiB); la salida normalizada no supera 1 MiB.</summary>
    public const long LimiteBytes = 25 * 1024 * 1024;

    /// <summary>Tipos MIME aceptados.</summary>
    public static readonly string[] ContentTypesPermitidos = ["image/jpeg", "image/png"];

    private static readonly byte[] _firmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _firmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Indica si el contenido es una imagen aceptable y coherente con su content-type.
    /// </summary>
    public static bool EsImagenValida(byte[] contenido, string contentType)
    {
        if (contenido is null || contenido.Length == 0 || contenido.Length > LimiteBytes)
        {
            return false;
        }

        return contentType switch
        {
            "image/jpeg" => EmpiezaCon(contenido, _firmaJpeg),
            "image/png" => EmpiezaCon(contenido, _firmaPng),
            _ => false,
        };
    }

    private static bool EmpiezaCon(byte[] contenido, byte[] firma)
    {
        if (contenido.Length < firma.Length)
        {
            return false;
        }

        for (var i = 0; i < firma.Length; i++)
        {
            if (contenido[i] != firma[i])
            {
                return false;
            }
        }

        return true;
    }
}
