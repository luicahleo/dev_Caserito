namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Resultado de la comparación facial, traducido a primitivos para que el dominio no dependa
/// del adaptador HTTP ni de sus tipos.
/// </summary>
/// <param name="ServicioRespondio">false si el servicio no respondió o falló al procesar.</param>
/// <param name="RostroDetectado">false si no se detectó rostro en alguna imagen.</param>
/// <param name="Coinciden">Veredicto del servicio, con su propio umbral ya aplicado.</param>
/// <param name="Score">Similitud 0-100, o null si no hubo comparación.</param>
public sealed record EntradaResolucionKyc(
    bool ServicioRespondio,
    bool RostroDetectado,
    bool Coinciden,
    double? Score);

/// <summary>Decisión tomada: qué resolución aplicar y, si queda en revisión, por qué.</summary>
public sealed record DecisionKyc(ResolucionKyc Resolucion, MotivoRevisionKyc? Motivo);

/// <summary>
/// Regla que convierte el resultado de la comparación facial en una resolución. El corte inferior
/// lo decide el servicio externo con su propio umbral; aquí solo se añade el corte superior a
/// partir del cual la aprobación es automática.
/// </summary>
public static class PoliticaResolucionKyc
{
    public static DecisionKyc Decidir(EntradaResolucionKyc entrada, double umbralAutoAprobacion)
    {
        if (!entrada.ServicioRespondio)
        {
            return new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.ServicioNoDisponible);
        }

        if (!entrada.RostroDetectado)
        {
            return new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.RostroNoDetectado);
        }

        if (!entrada.Coinciden)
        {
            return new DecisionKyc(ResolucionKyc.RechazarAutomatico, null);
        }

        return entrada.Score is { } score && score >= umbralAutoAprobacion
            ? new DecisionKyc(ResolucionKyc.AprobarAutomatico, null)
            : new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.ScoreInsuficiente);
    }
}
