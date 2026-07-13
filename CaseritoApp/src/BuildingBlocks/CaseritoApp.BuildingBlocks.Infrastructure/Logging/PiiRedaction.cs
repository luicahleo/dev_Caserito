namespace CaseritoApp.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Enmascara campos considerados PII sensible para que nunca aparezcan en logs.
/// Referencia del andamiaje; Fase 1 la reubica y la conecta a una
/// destructuring policy de Serilog. Ver política anti-PII en CLAUDE.md.
/// </summary>
public static class PiiRedaction
{
    public static IReadOnlySet<string> CamposProhibidos { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ci",
            "documentoNumero",
            "documentoImagen",
            "selfie",
            "token",
            "qr",
            "pagoReferencia",
        };

    public static string Redactar(string campo, string valor) =>
        CamposProhibidos.Contains(campo) ? "***" : valor;
}
