namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Estado del ciclo de vida de un aviso.</summary>
public enum EstadoAviso
{
    /// <summary>Visible y disponible.</summary>
    Activo,

    /// <summary>Oculto temporalmente por el dueño.</summary>
    Pausado,

    /// <summary>Eliminado (soft-delete); estado terminal.</summary>
    Eliminado,

    /// <summary>Vendido; estado comercial terminal.</summary>
    Vendido,
}
