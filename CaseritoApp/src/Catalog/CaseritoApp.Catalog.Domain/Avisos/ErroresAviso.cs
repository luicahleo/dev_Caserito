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

    /// <summary>El precio no es válido (400).</summary>
    public const string PrecioInvalido = "avisos.precio_invalido";

    /// <summary>La transición de estado solicitada no es válida para el estado actual (409).</summary>
    public const string TransicionInvalida = "avisos.transicion_invalida";
}
