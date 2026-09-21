namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Qué hacer con una solicitud recién enviada, según la evidencia facial.</summary>
public enum ResolucionKyc
{
    /// <summary>Evidencia clara a favor: se aprueba sin intervención humana.</summary>
    AprobarAutomatico,

    /// <summary>Evidencia insuficiente o no concluyente: la revisa una persona.</summary>
    EnviarARevision,

    /// <summary>Evidencia clara en contra: se rechaza sin intervención humana.</summary>
    RechazarAutomatico,
}
