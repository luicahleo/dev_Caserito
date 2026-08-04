using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Reserva opaca de un CI. Ninguna representación textual expone sus datos.</summary>
public sealed class DocumentoKycRegistrado : Entity
{
    private DocumentoKycRegistrado()
    {
        HuellaCi = string.Empty;
        NumeroCiCifrado = string.Empty;
    }

    public DocumentoKycRegistrado(
        Guid usuarioId,
        string huellaCi,
        string numeroCiCifrado,
        string? complementoCiCifrado,
        DepartamentoBolivia departamentoExpedicion,
        DateTimeOffset registradoEn)
    {
        UsuarioId = usuarioId;
        HuellaCi = huellaCi;
        NumeroCiCifrado = numeroCiCifrado;
        ComplementoCiCifrado = complementoCiCifrado;
        DepartamentoExpedicion = departamentoExpedicion;
        RegistradoEn = registradoEn;
    }

    public Guid UsuarioId { get; private init; }
    public string HuellaCi { get; private init; }
    public string NumeroCiCifrado { get; private init; }
    public string? ComplementoCiCifrado { get; private init; }
    public DepartamentoBolivia DepartamentoExpedicion { get; private init; }
    public DateTimeOffset RegistradoEn { get; private init; }

    public override string ToString() => $"DocumentoKycRegistrado(Id={Id}, UsuarioId={UsuarioId})";
}
