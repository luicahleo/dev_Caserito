using CaseritoApp.Host;
using Microsoft.AspNetCore.Http;

namespace CaseritoApp.IntegrationTests;

public sealed class PwaCachePolicyTests
{
    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/avisos/123", "text/html; charset=utf-8")]
    [InlineData("/index.html", "text/html")]
    [InlineData("/sw.js", "text/javascript")]
    [InlineData("/registerSW.js", "text/javascript")]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    public void Documento_de_arranque_pwa_requiere_revalidacion(string ruta, string tipoContenido)
    {
        Assert.True(PwaCachePolicy.RequiereRevalidacion(new PathString(ruta), tipoContenido));
    }

    [Theory]
    [InlineData("/assets/index-abc123.js", "text/javascript")]
    [InlineData("/pwa-192x192.png", "image/png")]
    [InlineData("/api/avisos", "application/json")]
    public void Assets_versionados_y_api_conservan_su_politica_de_cache(
        string ruta,
        string tipoContenido)
    {
        Assert.False(PwaCachePolicy.RequiereRevalidacion(new PathString(ruta), tipoContenido));
    }
}
