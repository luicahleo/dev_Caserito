namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Códigos de error de dominio del contexto KYC.</summary>
public static class ErroresKyc
{
    public const string SolicitudPendienteExiste = "Kyc.SolicitudPendienteExiste";
    public const string YaVerificado = "Kyc.YaVerificado";
    public const string SolicitudNoEncontrada = "Kyc.SolicitudNoEncontrada";
    public const string TransicionInvalida = "Kyc.TransicionInvalida";
    public const string ConflictoConcurrencia = "Kyc.ConflictoConcurrencia";
    public const string VerificacionFacialFallida = "Kyc.VerificacionFacialFallida";
    public const string ServicioVerificacionNoDisponible = "Kyc.ServicioVerificacionNoDisponible";
    public const string RostroNoDetectado = "Kyc.RostroNoDetectado";
}
