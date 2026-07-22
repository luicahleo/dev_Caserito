using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ModeracionChatHandlerTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reportar_conversacion_deriva_objetivo_y_agrega_reporte()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var reportes = new ReportesFake();
        var handler = new ReportarChatCommandHandler(
            new ConversacionesFake(conversacion), new MensajesFake(), reportes, new RelojFijo(_ahora));

        var resultado = await handler.Handle(new ReportarChatCommand(
            conversacion.Id,
            conversacion.CompradorId,
            TipoObjetivoReporteChat.Conversacion,
            null,
            CategoriaReporteChat.Acoso,
            "  Detalle opcional  "), CancellationToken.None);

        Assert.True(resultado.EsExito);
        var reporte = Assert.Single(reportes.Agregados);
        Assert.Equal(conversacion.Id, reporte.ConversacionId);
        Assert.Equal(conversacion.CompradorId, reporte.ReportanteId);
        Assert.Equal(TipoObjetivoReporteChat.Conversacion, reporte.TipoObjetivo);
        Assert.Null(reporte.MensajeId);
    }

    [Fact]
    public async Task Reportar_mensaje_valida_que_pertenezca_a_la_conversacion()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var mensaje = conversacion.CrearMensaje(
            conversacion.CompradorId, Guid.NewGuid(), 1, "contenido", _ahora).Valor;
        var mensajes = new MensajesFake(mensaje);
        var reportes = new ReportesFake();
        var handler = new ReportarChatCommandHandler(
            new ConversacionesFake(conversacion), mensajes, reportes, new RelojFijo(_ahora));

        var resultado = await handler.Handle(new ReportarChatCommand(
            conversacion.Id,
            conversacion.VendedorId,
            TipoObjetivoReporteChat.Mensaje,
            mensaje.Id,
            CategoriaReporteChat.Spam,
            null), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(1, mensajes.ConsultasPorId);
        Assert.Equal(mensaje.Id, Assert.Single(reportes.Agregados).MensajeId);
    }

    [Fact]
    public async Task Reportar_rechaza_duplicado_abierto()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var reportes = new ReportesFake(existeAbierto: true);
        var handler = new ReportarChatCommandHandler(
            new ConversacionesFake(conversacion), new MensajesFake(), reportes, new RelojFijo(_ahora));

        var resultado = await handler.Handle(new ReportarChatCommand(
            conversacion.Id, conversacion.CompradorId, TipoObjetivoReporteChat.Conversacion,
            null, CategoriaReporteChat.Acoso, null), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresModeracionChat.TransicionInvalida, resultado.Error.Code);
        Assert.Empty(reportes.Agregados);
    }

    private sealed class ConversacionesFake(Conversacion conversacion) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == conversacion.Id ? conversacion : null);

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) => Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion)
        {
        }
    }

    private sealed class MensajesFake(Mensaje? mensaje = null) : IRepositorioMensajes
    {
        public int ConsultasPorId { get; private set; }

        public Task<Mensaje?> ObtenerAsync(Guid id, CancellationToken ct)
        {
            ConsultasPorId++;
            return Task.FromResult(mensaje?.Id == id ? mensaje : null);
        }

        public Task<Mensaje?> ObtenerPorClaveAsync(
            Guid conversacionId, Guid remitenteId, Guid clave, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);

        public Task<long> ReservarSecuenciaAsync(CancellationToken ct) => Task.FromResult(1L);

        public void Agregar(Mensaje mensaje)
        {
        }
    }

    private sealed class ReportesFake(bool existeAbierto = false) : IRepositorioReportesChat
    {
        public List<ReporteChat> Agregados { get; } = [];

        public Task<bool> ExisteAbiertoAsync(
            Guid conversacionId,
            Guid reportanteId,
            TipoObjetivoReporteChat tipoObjetivo,
            Guid? mensajeId,
            CancellationToken ct) => Task.FromResult(existeAbierto);

        public void Agregar(ReporteChat reporte) => Agregados.Add(reporte);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
