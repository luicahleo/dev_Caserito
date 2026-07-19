namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Estado impuesto por moderación, independiente del ciclo de vida decidido por el dueño.</summary>
public enum EstadoModeracionAviso
{
    /// <summary>Sin restricciones de moderación.</summary>
    Visible,

    /// <summary>Retirado temporalmente de las superficies públicas.</summary>
    Oculto,

    /// <summary>Retirado de forma terminal por moderación.</summary>
    EliminadoPorModeracion,
}
