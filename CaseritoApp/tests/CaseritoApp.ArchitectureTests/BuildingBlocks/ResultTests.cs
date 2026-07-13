using CaseritoApp.BuildingBlocks.Domain;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class ResultTests
{
    [Fact]
    public void Exito_marca_es_exito_y_error_none()
    {
        var resultado = Result.Exito();
        Assert.True(resultado.EsExito);
        Assert.Equal(Error.None, resultado.Error);
    }

    [Fact]
    public void Fallo_marca_no_exito_y_conserva_error()
    {
        var error = new Error("codigo", "mensaje");
        var resultado = Result.Fallo(error);
        Assert.False(resultado.EsExito);
        Assert.Equal(error, resultado.Error);
    }

    [Fact]
    public void ResultT_exito_expone_valor()
    {
        Result<int> resultado = 42;
        Assert.True(resultado.EsExito);
        Assert.Equal(42, resultado.Valor);
    }

    [Fact]
    public void ResultT_fallo_no_es_exito()
    {
        var resultado = Result<int>.Fallo(new Error("x", "y"));
        Assert.False(resultado.EsExito);
    }
}
