using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ModeracionDominioTests
{
    private static readonly DateTime _ahora = new(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);

    private static Aviso NuevoAviso(Guid? vendedorId = null) => Aviso.Crear(
        vendedorId ?? Guid.NewGuid(),
        "Bicicleta",
        "Rodado 29",
        Dinero.Crear(1000m, Moneda.BOB).Valor,
        Guid.NewGuid(),
        Guid.NewGuid(),
        CondicionArticulo.Usado,
        _ahora);

    [Fact]
    public void Aviso_nace_visible()
    {
        Assert.Equal(EstadoModeracionAviso.Visible, NuevoAviso().EstadoModeracion);
    }

    [Fact]
    public void Ocultar_y_restaurar_cambian_solo_el_estado_de_moderacion()
    {
        var aviso = NuevoAviso();

        Assert.True(aviso.OcultarPorModeracion(_ahora.AddMinutes(1)).EsExito);
        Assert.Equal(EstadoModeracionAviso.Oculto, aviso.EstadoModeracion);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);

        Assert.True(aviso.RestaurarPorModeracion(_ahora.AddMinutes(2)).EsExito);
        Assert.Equal(EstadoModeracionAviso.Visible, aviso.EstadoModeracion);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
    }

    [Fact]
    public void Eliminado_por_moderacion_es_terminal()
    {
        var aviso = NuevoAviso();

        Assert.True(aviso.EliminarPorModeracion(_ahora).EsExito);
        Assert.Equal(EstadoModeracionAviso.EliminadoPorModeracion, aviso.EstadoModeracion);

        var ocultar = aviso.OcultarPorModeracion(_ahora);
        var restaurar = aviso.RestaurarPorModeracion(_ahora);
        var eliminar = aviso.EliminarPorModeracion(_ahora);

        Assert.Equal(ErroresAviso.TransicionModeracionInvalida, ocultar.Error.Code);
        Assert.Equal(ErroresAviso.TransicionModeracionInvalida, restaurar.Error.Code);
        Assert.Equal(ErroresAviso.TransicionModeracionInvalida, eliminar.Error.Code);
    }

    [Fact]
    public void Reporte_nace_pendiente_y_puede_atenderse()
    {
        var moderadorId = Guid.NewGuid();
        var reporte = ReporteAviso.Crear(
            Guid.NewGuid(), Guid.NewGuid(), MotivoReporteAviso.EstafaOEngano, "Contexto", _ahora);

        var resultado = reporte.Atender(moderadorId, _ahora.AddMinutes(3));

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoReporteAviso.Atendido, reporte.Estado);
        Assert.Equal(moderadorId, reporte.ResueltoPorId);
        Assert.Equal(_ahora.AddMinutes(3), reporte.FechaResolucion);
    }

    [Fact]
    public void Reporte_resuelto_no_puede_resolverse_de_nuevo()
    {
        var reporte = ReporteAviso.Crear(
            Guid.NewGuid(), Guid.NewGuid(), MotivoReporteAviso.Otro, null, _ahora);
        reporte.Descartar(Guid.NewGuid(), _ahora);

        var resultado = reporte.Atender(Guid.NewGuid(), _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.ReporteYaResuelto, resultado.Error.Code);
    }

    [Fact]
    public void Registro_de_moderacion_conserva_solo_identificadores_accion_y_fecha()
    {
        var avisoId = Guid.NewGuid();
        var moderadorId = Guid.NewGuid();

        var registro = RegistroModeracion.Crear(
            avisoId, moderadorId, AccionModeracionAviso.Ocultar, null, _ahora);

        Assert.Equal(avisoId, registro.AvisoId);
        Assert.Equal(moderadorId, registro.ModeradorId);
        Assert.Equal(AccionModeracionAviso.Ocultar, registro.Accion);
        Assert.Null(registro.ReporteId);
        Assert.Equal(_ahora, registro.Fecha);
    }

    [Fact]
    public void Aviso_oculto_sigue_siendo_editable_por_el_dueno()
    {
        var aviso = NuevoAviso();
        aviso.OcultarPorModeracion(_ahora);

        var resultado = aviso.Editar(
            "Título corregido", "Descripción corregida", Dinero.Crear(900m, Moneda.BOB).Valor,
            aviso.CategoriaId, aviso.CiudadId, aviso.Condicion, _ahora.AddMinutes(1));

        Assert.True(resultado.EsExito);
        Assert.Equal("Título corregido", aviso.Titulo);
    }

    [Fact]
    public void Aviso_eliminado_por_moderacion_bloquea_mutaciones_del_dueno()
    {
        var aviso = NuevoAviso();
        aviso.AgregarFoto("clave", "image/jpeg");
        var fotoId = aviso.Fotos[0].Id;
        aviso.EliminarPorModeracion(_ahora);

        var editar = aviso.Editar(
            "Título", "Descripción", aviso.Precio,
            aviso.CategoriaId, aviso.CiudadId, aviso.Condicion, _ahora);

        Assert.Equal(ErroresAviso.EliminadoPorModeracion, editar.Error.Code);
        Assert.Equal(ErroresAviso.EliminadoPorModeracion, aviso.Pausar(_ahora).Error.Code);
        Assert.Equal(ErroresAviso.EliminadoPorModeracion, aviso.Reactivar(_ahora).Error.Code);
        Assert.Equal(ErroresAviso.EliminadoPorModeracion, aviso.Eliminar(_ahora).Error.Code);
        Assert.Equal(ErroresAviso.EliminadoPorModeracion, aviso.AgregarFoto("otra", "image/jpeg").Error.Code);
        Assert.Equal(ErroresAviso.EliminadoPorModeracion, aviso.QuitarFoto(fotoId).Error.Code);
    }
}
