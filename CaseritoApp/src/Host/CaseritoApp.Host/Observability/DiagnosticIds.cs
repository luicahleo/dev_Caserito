using System.Security.Cryptography;

namespace CaseritoApp.Host.Observability;

public static class DiagnosticIds
{
    public static string NewErrorId() => $"ERR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";

    public static bool IsSessionId(string? value) =>
        value is not null
        && value.Length == 16
        && value.StartsWith("SES-", StringComparison.Ordinal)
        && value.AsSpan(4).ToString().All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
