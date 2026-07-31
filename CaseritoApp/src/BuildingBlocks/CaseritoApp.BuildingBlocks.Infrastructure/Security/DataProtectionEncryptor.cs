using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Encryptor real basado en ASP.NET Core Data Protection (con rotación de claves incorporada).
/// Reemplaza al PassthroughEncryptor fuera de Development/Testing para que la PII de KYC (CI,
/// selfies) no se escriba en claro. El key ring se persiste por volumen (config
/// "DataProtection:RutaClaves"); sin ese volumen los datos cifrados no sobreviven un recreate del
/// contenedor. El envelope/KMS externo sigue diferido: cambiar la implementación registrada en DI
/// no afecta a los consumidores de <see cref="IEncryptor"/>.
/// </summary>
public sealed class DataProtectionEncryptor : IEncryptor
{
    private readonly IDataProtector _protector;

    public DataProtectionEncryptor(IDataProtectionProvider proveedor)
    {
        _protector = proveedor.CreateProtector("CaseritoApp.KycPii.v1");
    }

    public string Cifrar(string textoPlano)
        => Base64Url.EncodeToString(Cifrar(Encoding.UTF8.GetBytes(textoPlano)));

    public byte[] Cifrar(byte[] datos) => _protector.Protect(datos);

    public string Descifrar(string textoCifrado)
        => Encoding.UTF8.GetString(Descifrar(Base64Url.DecodeFromChars(textoCifrado)));

    public byte[] Descifrar(byte[] datos) => _protector.Unprotect(datos);
}
