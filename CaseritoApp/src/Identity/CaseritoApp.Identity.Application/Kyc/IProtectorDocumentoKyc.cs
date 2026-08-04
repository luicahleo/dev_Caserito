using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

public sealed record DocumentoKycProtegido(
    string HuellaCi,
    string NumeroCiCifrado,
    string? ComplementoCiCifrado,
    DepartamentoBolivia DepartamentoExpedicion);

public interface IProtectorDocumentoKyc
{
    public DocumentoKycProtegido Proteger(
        string numeroCi,
        string? complementoCi,
        DepartamentoBolivia departamentoExpedicion);

    public string Descifrar(string valorCifrado);
}
