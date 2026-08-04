using System.Security.Cryptography;
using System.Text;
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

public sealed class ProtectorDocumentoKyc(
    IEncryptor encryptor,
    IConfiguration configuracion,
    IHostEnvironment entorno)
    : IProtectorDocumentoKyc
{
    public DocumentoKycProtegido Proteger(
        string numeroCi,
        string? complementoCi,
        DepartamentoBolivia departamentoExpedicion)
    {
        var numero = Canonicalizar(numeroCi);
        var complemento = string.IsNullOrWhiteSpace(complementoCi) ? null : Canonicalizar(complementoCi);
        var clave = configuracion["Kyc:ClaveHuellaCi"];
        if (string.IsNullOrWhiteSpace(clave) && (entorno.IsDevelopment() || entorno.IsEnvironment("Testing")))
        {
            clave = "clave-local-solo-desarrollo-y-pruebas";
        }
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new InvalidOperationException("La protección documental KYC no está configurada.");
        }

        var canonico = $"{numero}|{complemento}|{departamentoExpedicion}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clave));
        var huella = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonico)));
        return new(
            huella,
            encryptor.Cifrar(numeroCi.Trim()),
            complementoCi is null ? null : encryptor.Cifrar(complementoCi.Trim()),
            departamentoExpedicion);
    }

    public string Descifrar(string valorCifrado) => encryptor.Descifrar(valorCifrado);

    private static string Canonicalizar(string valor) =>
        string.Concat(valor.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
