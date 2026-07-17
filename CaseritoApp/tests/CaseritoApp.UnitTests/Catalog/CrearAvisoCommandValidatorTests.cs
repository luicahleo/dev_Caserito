using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class CrearAvisoCommandValidatorTests
{
    private static CrearAvisoCommand Valido() => new(
        Guid.NewGuid(), true, "Titulo", "Descripcion", 100m, "Nuevo", Guid.NewGuid(), Guid.NewGuid());

    private readonly CrearAvisoCommandValidator _validator = new();

    [Fact]
    public void Comando_valido_pasa()
    {
        Assert.True(_validator.Validate(Valido()).IsValid);
    }

    [Fact]
    public void Titulo_vacio_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Titulo = "" }).IsValid);
    }

    [Fact]
    public void Monto_no_positivo_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Monto = 0 }).IsValid);
    }

    [Fact]
    public void Condicion_no_reconocida_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Condicion = "Roto" }).IsValid);
    }

    [Fact]
    public void Categoria_vacia_falla()
    {
        Assert.False(_validator.Validate(Valido() with { CategoriaId = Guid.Empty }).IsValid);
    }
}
