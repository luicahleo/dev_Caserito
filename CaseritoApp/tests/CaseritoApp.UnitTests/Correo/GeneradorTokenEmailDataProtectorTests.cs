using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class GeneradorTokenEmailDataProtectorTests
{
    private static GeneradorTokenEmailDataProtector Crear(
        TimeProvider reloj,
        TimeSpan? vigencia = null)
    {
        var provider = DataProtectionProvider.Create("CaseritoTest");
        return new GeneradorTokenEmailDataProtector(provider, reloj, vigencia);
    }

    [Fact]
    public void Generar_y_validar_token_devuelve_usuarioId()
    {
        var generador = Crear(TimeProvider.System);
        var usuarioId = Guid.NewGuid();
        var token = generador.Generar(usuarioId);

        Assert.True(generador.Validar(token, out var resultado));
        Assert.Equal(usuarioId, resultado);
    }

    [Fact]
    public void Token_es_valido_antes_de_24_horas_y_caduca_despues()
    {
        var reloj = new RelojMutable(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
        var generador = Crear(reloj);
        var token = generador.Generar(Guid.NewGuid());

        reloj.Avanzar(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        Assert.True(generador.Validar(token, out _));

        reloj.Avanzar(TimeSpan.FromSeconds(2));
        Assert.False(generador.Validar(token, out _));
    }

    [Fact]
    public void Token_con_fecha_futura_no_es_valido()
    {
        var reloj = new RelojMutable(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
        var generador = Crear(reloj);
        var token = generador.Generar(Guid.NewGuid());

        reloj.Avanzar(TimeSpan.FromMinutes(-1));

        Assert.False(generador.Validar(token, out _));
    }

    [Fact]
    public void Token_invalido_no_es_valido()
    {
        var generador = Crear(TimeProvider.System);
        Assert.False(generador.Validar("token-invalido", out _));
    }

    private sealed class RelojMutable(DateTimeOffset ahora) : TimeProvider
    {
        private DateTimeOffset _ahora = ahora;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
    }
}
