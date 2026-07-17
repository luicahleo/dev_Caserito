using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class QueriesAvisoHandlerTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso aviso) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>(
                [new AvisoResumenDto(Guid.NewGuid(), "T", 1m, "BOB", Guid.NewGuid(), Guid.NewGuid(), "Nuevo", "Activo", _ahora)],
                pagina, tamano, 1));
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    [Fact]
    public async Task ListarMisAvisos_devuelve_pagina()
    {
        var handler = new ListarMisAvisosQueryHandler(new RepositorioFake(null));

        var r = await handler.Handle(new ListarMisAvisosQuery(Guid.NewGuid(), 1, 20), CancellationToken.None);

        Assert.Single(r.Items);
        Assert.Equal(1, r.Total);
    }

    [Fact]
    public async Task ObtenerMiAviso_ok_si_dueno()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(aviso.Id, r.Valor.Id);
        Assert.Equal("BOB", r.Valor.Moneda);
    }

    [Fact]
    public async Task ObtenerMiAviso_404_si_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }

    [Fact]
    public async Task ObtenerMiAviso_403_si_no_dueno()
    {
        var aviso = AvisoDe(Guid.NewGuid());
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEsPropietario, r.Error.Code);
    }

    [Fact]
    public void ListarMisAvisos_validator_rechaza_tamano_excesivo()
    {
        var validator = new ListarMisAvisosQueryValidator();
        Assert.False(validator.Validate(new ListarMisAvisosQuery(Guid.NewGuid(), 1, 500)).IsValid);
    }
}
