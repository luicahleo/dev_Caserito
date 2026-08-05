using System.Security.Cryptography;

namespace CaseritoApp.Host.Observability;

public static class DiagnosticIds
{
    public static string NewErrorId() => $"ERR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";
}
