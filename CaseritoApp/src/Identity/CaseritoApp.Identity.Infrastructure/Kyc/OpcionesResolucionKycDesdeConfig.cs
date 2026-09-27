using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Lee el umbral de auto-aprobación de la sección de configuración de ARGOS.</summary>
public sealed class OpcionesResolucionKycDesdeConfig(IOptions<OpcionesArgos> opciones)
    : IOpcionesResolucionKyc
{
    public double UmbralAutoAprobacionSimilitud => opciones.Value.UmbralAutoAprobacion;
}
