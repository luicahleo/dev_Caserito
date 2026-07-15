namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Cifra/descifra campos sensibles (envelope encryption). La implementación
/// real se cablea al elegir KMS/hosting; en dev se usa PassthroughEncryptor.</summary>
public interface IEncryptor
{
    public string Cifrar(string textoPlano);
    public byte[] Cifrar(byte[] datos);
    public string Descifrar(string textoCifrado);
    public byte[] Descifrar(byte[] datos);
}
