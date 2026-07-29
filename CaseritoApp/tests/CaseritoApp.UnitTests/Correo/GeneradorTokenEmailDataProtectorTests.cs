using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class GeneradorTokenEmailDataProtectorTests
{
    private static GeneradorTokenEmailDataProtector Crear(TimeSpan? vigencia = null)
    {
        var provider = DataProtectionProvider.Create("CaseritoTest");
        return new GeneradorTokenEmailDataProtector(provider, vigencia);
    }

    [Fact]
    public void Generar_y_validar_token_devuelve_usuarioId()
    {
        var generador = Crear();
        var usuarioId = Guid.NewGuid();
        var token = generador.Generar(usuarioId);

        Assert.True(generador.Validar(token, out var resultado));
        Assert.Equal(usuarioId, resultado);
    }

    [Fact]
    public void Token_expirado_no_es_valido()
    {
        var generador = Crear(TimeSpan.FromSeconds(-1));
        var token = generador.Generar(Guid.NewGuid());

        Assert.False(generador.Validar(token, out _));
    }

    [Fact]
    public void Token_invalido_no_es_valido()
    {
        var generador = Crear();
        Assert.False(generador.Validar("token-invalido", out _));
    }
}
