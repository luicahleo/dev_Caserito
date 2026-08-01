using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CaseritoApp.UnitTests.Auth;

public sealed class GestorLoginExternoPendienteTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoginExterno_valido_hace_round_trip()
    {
        var gestor = CrearGestor(new RelojMutable(_ahora));
        var pendiente = CrearPendiente();

        var ticket = gestor.Proteger(pendiente);
        var valido = gestor.TryDesproteger(ticket, out var recuperado);

        Assert.True(valido);
        Assert.Equal(pendiente, recuperado);
    }

    [Fact]
    public void Ticket_expirado_o_manipulado_falla_sin_exponer_datos()
    {
        var reloj = new RelojMutable(_ahora);
        var gestor = CrearGestor(reloj);
        var ticket = gestor.Proteger(CrearPendiente());

        reloj.Avanzar(TimeSpan.FromMinutes(11));

        Assert.False(gestor.TryDesproteger(ticket, out _));
        Assert.False(gestor.TryDesproteger(ticket + "alterado", out _));
    }

    [Theory]
    [InlineData("github", "/perfil")]
    [InlineData("google", "https://sitio.example")]
    [InlineData("facebook", "//sitio.example")]
    [InlineData("google", "/\\sitio.example")]
    public void Proveedor_o_retorno_invalido_se_rechaza(string proveedor, string retorno)
    {
        var gestor = CrearGestor(new RelojMutable(_ahora));
        var pendiente = CrearPendiente(proveedor, retorno);

        Assert.Throws<ArgumentException>(() => gestor.Proteger(pendiente));
    }

    [Fact]
    public void Proyeccion_solo_informa_datos_pendientes_sin_pii()
    {
        var gestor = CrearGestor(new RelojMutable(_ahora));
        var pendiente = CrearPendiente(email: "persona@caserito.test", nombre: null);

        var proyeccion = gestor.Proyectar(pendiente, requiereVinculacion: true);
        var serializado = System.Text.Json.JsonSerializer.Serialize(proyeccion);

        Assert.False(proyeccion.RequiereEmail);
        Assert.True(proyeccion.RequiereNombre);
        Assert.True(proyeccion.RequiereCiudad);
        Assert.True(proyeccion.RequiereVinculacion);
        Assert.DoesNotContain("persona@caserito.test", serializado, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-externa", serializado, StringComparison.Ordinal);
    }

    [Fact]
    public void Facebook_nunca_marca_el_email_como_confiable()
    {
        var gestor = CrearGestor(new RelojMutable(_ahora));
        var ticket = gestor.Proteger(CrearPendiente(proveedor: "Facebook"));

        Assert.True(gestor.TryDesproteger(ticket, out var recuperado));
        Assert.Equal("facebook", recuperado!.Proveedor);
        Assert.False(recuperado.EmailConfiable);
    }

    [Fact]
    public void Cookie_pendiente_es_http_only_corta_y_restringida()
    {
        var gestor = CrearGestor(new RelojMutable(_ahora));
        var contexto = new DefaultHttpContext();

        gestor.GuardarCookie(contexto.Response, CrearPendiente(), segura: true);

        var cookie = contexto.Response.Headers.SetCookie.ToString();
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth/external", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=600", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static GestorLoginExternoPendienteDataProtector CrearGestor(TimeProvider reloj)
    {
        var proveedor = DataProtectionProvider.Create($"Caserito-login-externo-{Guid.NewGuid():N}");
        return new GestorLoginExternoPendienteDataProtector(proveedor, reloj);
    }

    private static LoginExternoPendiente CrearPendiente(
        string proveedor = "google",
        string retorno = "/perfil",
        string? email = "persona@caserito.test",
        string? nombre = "Persona") =>
        new(proveedor, "clave-externa", email, true, nombre, retorno, _ahora.AddMinutes(10));

    private sealed class RelojMutable(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;

        public void Avanzar(TimeSpan periodo) => ahora += periodo;
    }
}
