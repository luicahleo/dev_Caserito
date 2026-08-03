namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Opciones globales de la aplicación (sección de configuración <c>App</c>).</summary>
public sealed class OpcionesApp
{
    public const string Seccion = "App";

    /// <summary>
    /// URL pública base de la SPA, usada para construir enlaces en correos. En producción:
    /// https://caserito.app. Default: dev server de Vite.
    /// </summary>
    public string UrlPublica { get; set; } = "http://localhost:5173";
}
