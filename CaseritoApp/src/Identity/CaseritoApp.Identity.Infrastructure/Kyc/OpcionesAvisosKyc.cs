using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Configuración de los avisos de KYC a la administración (sección <c>Kyc</c>).</summary>
public sealed class OpcionesAvisosKyc
{
    public const string Seccion = "Kyc";

    /// <summary>
    /// Buzón al que se avisa de las solicitudes que esperan revisión humana. Puede ser un alias
    /// que reparta internamente. Vacío desactiva el aviso.
    /// </summary>
    public string? EmailAvisos { get; set; }
}

/// <summary>Lee el buzón de avisos de la sección <c>Kyc</c>.</summary>
public sealed class OpcionesAvisosKycDesdeConfig(IOptions<OpcionesAvisosKyc> opciones)
    : IOpcionesAvisosKyc
{
    public string? EmailAvisos => opciones.Value.EmailAvisos;
}
