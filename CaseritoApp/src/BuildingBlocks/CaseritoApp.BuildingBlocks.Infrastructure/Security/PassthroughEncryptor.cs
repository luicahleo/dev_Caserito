namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Implementación de desarrollo: NO cifra. Prohibido en producción;
/// se reemplaza por envelope encryption real al definir el KMS.</summary>
public sealed class PassthroughEncryptor : IEncryptor
{
    public string Cifrar(string textoPlano) => textoPlano;
    public string Descifrar(string textoCifrado) => textoCifrado;
}
