namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Opciones del almacén de blobs KYC en disco (sección de configuración <c>Kyc</c>).</summary>
public sealed class OpcionesAlmacenKyc
{
    public const string Seccion = "Kyc";

    /// <summary>Directorio base donde se escriben los blobs cifrados. Debe estar fuera del repositorio.</summary>
    public string RutaBase { get; set; } = Path.Combine(Path.GetTempPath(), "caserito-kyc");
}
