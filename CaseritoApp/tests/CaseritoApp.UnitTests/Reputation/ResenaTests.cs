using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.UnitTests.Reputation;

public sealed class ResenaTests
{
    [Fact]
    public void Crear_valida_y_normaliza_la_resena()
    {
        var orderId = Guid.NewGuid();
        var autorId = Guid.NewGuid();
        var destinatarioId = Guid.NewGuid();
        var ocurrioEn = new DateTimeOffset(2026, 7, 25, 14, 0, 0, TimeSpan.FromHours(-4));

        var resultado = Resena.Crear(
            orderId,
            autorId,
            destinatarioId,
            RolAutorResena.Comprador,
            5,
            "  Cumplió con todo lo acordado.  ",
            ocurrioEn);

        Assert.True(resultado.EsExito);
        var resena = resultado.Valor;
        Assert.Equal(orderId, resena.OrderId);
        Assert.Equal(autorId, resena.AutorId);
        Assert.Equal(destinatarioId, resena.DestinatarioId);
        Assert.Equal(RolAutorResena.Comprador, resena.RolAutor);
        Assert.Equal(5, resena.Puntuacion);
        Assert.Equal("Cumplió con todo lo acordado.", resena.Comentario);
        Assert.Equal(ocurrioEn.ToUniversalTime(), resena.CreadaEn);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Crear_admite_puntuaciones_limite(int puntuacion)
    {
        var resultado = CrearValida(puntuacion, new string('a', 10));

        Assert.True(resultado.EsExito);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Crear_rechaza_puntuacion_fuera_de_rango(int puntuacion)
    {
        var resultado = CrearValida(puntuacion, "Comentario válido.");

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    [Fact]
    public void Crear_admite_comentario_de_longitud_maxima()
    {
        var resultado = CrearValida(4, new string('a', 500));

        Assert.True(resultado.EsExito);
        Assert.Equal(500, resultado.Valor.Comentario.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("         ")]
    [InlineData("123456789")]
    public void Crear_rechaza_comentario_vacio_o_corto(string comentario)
    {
        var resultado = CrearValida(4, comentario);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    [Fact]
    public void Crear_rechaza_comentario_mayor_de_500()
    {
        var resultado = CrearValida(4, new string('a', 501));

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("autor")]
    [InlineData("destinatario")]
    public void Crear_rechaza_identificador_vacio(string campo)
    {
        var orderId = campo == "order" ? Guid.Empty : Guid.NewGuid();
        var autorId = campo == "autor" ? Guid.Empty : Guid.NewGuid();
        var destinatarioId = campo == "destinatario" ? Guid.Empty : Guid.NewGuid();

        var resultado = Resena.Crear(
            orderId,
            autorId,
            destinatarioId,
            RolAutorResena.Vendedor,
            4,
            "Comentario válido.",
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    [Fact]
    public void Crear_rechaza_participantes_iguales()
    {
        var participanteId = Guid.NewGuid();

        var resultado = Resena.Crear(
            Guid.NewGuid(),
            participanteId,
            participanteId,
            RolAutorResena.Comprador,
            4,
            "Comentario válido.",
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    [Fact]
    public void Crear_rechaza_rol_desconocido()
    {
        var resultado = Resena.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            (RolAutorResena)999,
            4,
            "Comentario válido.",
            DateTimeOffset.UtcNow);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresResena.Invalida, resultado.Error.Code);
    }

    private static CaseritoApp.BuildingBlocks.Domain.Result<Resena> CrearValida(
        int puntuacion,
        string comentario) =>
        Resena.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RolAutorResena.Comprador,
            puntuacion,
            comentario,
            DateTimeOffset.UtcNow);
}
