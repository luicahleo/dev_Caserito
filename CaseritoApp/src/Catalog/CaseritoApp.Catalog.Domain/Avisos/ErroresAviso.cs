namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Códigos de error de dominio del contexto Catalog. Sirven de título en el mapeo a HTTP.</summary>
public static class ErroresAviso
{
    /// <summary>El aviso no existe o está eliminado (404).</summary>
    public const string NoEncontrado = "avisos.no_encontrado";

    /// <summary>El usuario no es el dueño del aviso (403).</summary>
    public const string NoEsPropietario = "avisos.no_es_propietario";

    /// <summary>El usuario no está verificado y no puede publicar (403).</summary>
    public const string NoVerificado = "avisos.no_verificado";

    /// <summary>La categoría no existe o está inactiva (400).</summary>
    public const string CategoriaInvalida = "avisos.categoria_invalida";

    /// <summary>La ciudad no existe o está inactiva (400).</summary>
    public const string CiudadInvalida = "avisos.ciudad_invalida";

    /// <summary>La condición del artículo no es válida (400).</summary>
    public const string CondicionInvalida = "avisos.condicion_invalida";

    /// <summary>El precio no es válido (400).</summary>
    public const string PrecioInvalido = "avisos.precio_invalido";

    /// <summary>La transición de estado solicitada no es válida para el estado actual (409).</summary>
    public const string TransicionInvalida = "avisos.transicion_invalida";

    /// <summary>El aviso ya tiene el máximo de fotos permitidas — 5 (400).</summary>
    public const string LimiteFotosAlcanzado = "aviso.limite_fotos_alcanzado";

    /// <summary>La foto no pertenece al aviso indicado (400).</summary>
    public const string FotoNoEncontrada = "aviso.foto_no_encontrada";

    /// <summary>La imagen no supera la validación (magic bytes / tamaño / tipo MIME) (400).</summary>
    public const string ImagenInvalida = "aviso.imagen_invalida";

    /// <summary>Error al guardar o recuperar el blob de la foto (500).</summary>
    public const string ErrorAlmacenamiento = "aviso.error_almacenamiento";

    /// <summary>La transición de moderación no es válida para el estado actual (409).</summary>
    public const string TransicionModeracionInvalida = "avisos.transicion_moderacion_invalida";

    /// <summary>Una mutación del dueño intentó operar sobre un aviso eliminado por moderación (409).</summary>
    public const string EliminadoPorModeracion = "avisos.eliminado_por_moderacion";

    /// <summary>El reporte ya fue atendido o descartado (409).</summary>
    public const string ReporteYaResuelto = "avisos.reporte_ya_resuelto";
}
