using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class AvisoTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

    private static Aviso NuevoAviso() => Aviso.Crear(
        vendedorId: Guid.NewGuid(),
        titulo: "Bicicleta de montaña",
        descripcion: "Rodado 29, poco uso",
        precio: Dinero.Crear(1200m, Moneda.BOB).Valor,
        categoriaId: Guid.NewGuid(),
        ciudadId: Guid.NewGuid(),
        condicion: CondicionArticulo.Usado,
        ahoraUtc: _ahora);

    [Fact]
    public void Crear_nace_activo_y_emite_AvisoPublicado()
    {
        var aviso = NuevoAviso();

        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
        Assert.Equal(_ahora, aviso.FechaCreacion);
        Assert.Equal(_ahora, aviso.FechaActualizacion);
        Assert.Contains(aviso.EventosDeDominio, e => e is AvisoPublicado);
    }

    [Fact]
    public void Pausar_activo_pasa_a_pausado()
    {
        var aviso = NuevoAviso();

        var resultado = aviso.Pausar(_ahora.AddMinutes(5));

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoAviso.Pausado, aviso.Estado);
        Assert.Equal(_ahora.AddMinutes(5), aviso.FechaActualizacion);
    }

    [Fact]
    public void Pausar_ya_pausado_falla_con_transicion_invalida()
    {
        var aviso = NuevoAviso();
        aviso.Pausar(_ahora);

        var resultado = aviso.Pausar(_ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.TransicionInvalida, resultado.Error.Code);
    }

    [Fact]
    public void Reactivar_pausado_pasa_a_activo()
    {
        var aviso = NuevoAviso();
        aviso.Pausar(_ahora);

        var resultado = aviso.Reactivar(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
    }

    [Fact]
    public void Eliminar_marca_eliminado_y_es_terminal()
    {
        var aviso = NuevoAviso();

        Assert.True(aviso.Eliminar(_ahora).EsExito);
        Assert.Equal(EstadoAviso.Eliminado, aviso.Estado);

        Assert.False(aviso.Eliminar(_ahora).EsExito);
        Assert.False(aviso.Pausar(_ahora).EsExito);
        Assert.False(aviso.Reactivar(_ahora).EsExito);
    }

    [Fact]
    public void Editar_eliminado_falla()
    {
        var aviso = NuevoAviso();
        aviso.Eliminar(_ahora);

        var resultado = aviso.Editar(
            "Nuevo titulo", "Nueva desc", Dinero.Crear(50m, Moneda.BOB).Valor,
            Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.TransicionInvalida, resultado.Error.Code);
    }

    [Fact]
    public void Editar_activo_actualiza_campos_y_fecha()
    {
        var aviso = NuevoAviso();
        var nuevaCategoria = Guid.NewGuid();

        var resultado = aviso.Editar(
            "Bici nueva", "Descripcion editada", Dinero.Crear(999m, Moneda.BOB).Valor,
            nuevaCategoria, aviso.CiudadId, CondicionArticulo.Nuevo, _ahora.AddHours(1));

        Assert.True(resultado.EsExito);
        Assert.Equal("Bici nueva", aviso.Titulo);
        Assert.Equal(999m, aviso.Precio.Monto);
        Assert.Equal(nuevaCategoria, aviso.CategoriaId);
        Assert.Equal(CondicionArticulo.Nuevo, aviso.Condicion);
        Assert.Equal(_ahora.AddHours(1), aviso.FechaActualizacion);
    }
}
