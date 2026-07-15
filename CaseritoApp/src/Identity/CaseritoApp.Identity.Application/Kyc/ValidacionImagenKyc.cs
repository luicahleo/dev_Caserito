namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Reglas de aceptación de imágenes de KYC: whitelist de tipo MIME, límite de tamaño y verificación
/// de <em>magic bytes</em> (no se confía en la extensión ni en el content-type declarado).
/// </summary>
public static class ValidacionImagenKyc
{
    /// <summary>Tamaño máximo por archivo (5 MiB).</summary>
    public const long LimiteBytes = 5 * 1024 * 1024;

    /// <summary>Tipos MIME aceptados.</summary>
    public static readonly string[] ContentTypesPermitidos = ["image/jpeg", "image/png"];

    private static readonly byte[] _firmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _firmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Indica si el contenido es una imagen aceptable y coherente con su content-type.</summary>
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
