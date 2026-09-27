namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Buzón al que se avisa de las solicitudes que esperan revisión humana. Es un puerto porque
/// Application no referencia paquetes de configuración. Vacío desactiva el aviso: desarrollo y
/// test no necesitan buzón.
/// </summary>
public interface IOpcionesAvisosKyc
{
    /// <summary>Dirección de destino, o nulo/vacío para no avisar.</summary>
    public string? EmailAvisos { get; }
}
