using System.Buffers.Text;
using System.Globalization;
using System.Text;
using CaseritoApp.Identity.Application.Correo;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed class GeneradorTokenEmailDataProtector : IGeneradorTokenEmail
{
    private readonly IDataProtector _protector;
    private readonly TimeProvider _reloj;
    private readonly TimeSpan _vigencia;

    public GeneradorTokenEmailDataProtector(
        IDataProtectionProvider dataProtection,
        TimeProvider reloj,
        TimeSpan? vigencia = null)
    {
        _protector = dataProtection.CreateProtector("CaseritoApp.EmailConfirmation");
        _reloj = reloj;
        _vigencia = vigencia ?? TimeSpan.FromHours(24);
    }

    public string Generar(Guid usuarioId)
    {
        var payload = $"{usuarioId:N}|{_reloj.GetUtcNow():O}";
        var protegido = _protector.Protect(payload);
        return Base64UrlEncode(protegido);
    }

    public bool Validar(string token, out Guid usuarioId)
    {
        usuarioId = Guid.Empty;
        try
        {
            var protegido = Base64UrlDecode(token);
            var payload = _protector.Unprotect(protegido);
            var partes = payload.Split('|');
            if (partes.Length != 2 || !Guid.TryParseExact(partes[0], "N", out var parsedId))
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(partes[1], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var emitido))
            {
                return false;
            }

            var ahora = _reloj.GetUtcNow();
            if (emitido > ahora || ahora - emitido > _vigencia)
            {
                return false;
            }

            usuarioId = parsedId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Base64Url.EncodeToString(bytes);
    }

    private static string Base64UrlDecode(string input)
    {
        var bytes = Base64Url.DecodeFromChars(input.ToCharArray());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
