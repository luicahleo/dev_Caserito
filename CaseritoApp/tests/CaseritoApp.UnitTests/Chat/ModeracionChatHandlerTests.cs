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

    [Fact]
    public async Task Reportar_participante_deriva_contraparte_sin_aceptar_objetivo_cliente()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var reportes = new ReportesFake();

        var resultado = await new ReportarChatCommandHandler(
            new ConversacionesFake(conversacion), new MensajesFake(), reportes,
            new RelojFijo(_ahora)).Handle(new ReportarChatCommand(
                conversacion.Id, conversacion.CompradorId, TipoObjetivoReporteChat.Participante,
                null, CategoriaReporteChat.Acoso, null), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(TipoObjetivoReporteChat.Participante, Assert.Single(reportes.Agregados).TipoObjetivo);
        Assert.DoesNotContain(typeof(ReportarChatCommand).GetProperties(),
            p => p.Name.Contains("Participante", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reportar_objetivo_externo_es_indistinguible_de_ausente()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var externa = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var mensajeExterno = externa.CrearMensaje(
            externa.CompradorId, Guid.NewGuid(), 1, "contenido", _ahora).Valor;
        var handler = new ReportarChatCommandHandler(
            new ConversacionesFake(conversacion), new MensajesFake(mensajeExterno),
            new ReportesFake(), new RelojFijo(_ahora));

        var resultado = await handler.Handle(new ReportarChatCommand(
            conversacion.Id, conversacion.CompradorId, TipoObjetivoReporteChat.Mensaje,
            mensajeExterno.Id, CategoriaReporteChat.Acoso, null), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public async Task Tomar_reporte_pendiente_asigna_un_solo_moderador_y_audita()
    {
        var reporte = CrearReporte();
        var registros = new RegistrosFake();
        var moderadorId = Guid.NewGuid();
        var resultado = await new TomarReporteChatCommandHandler(
            new ReportesFake(reporte: reporte), registros, new RelojFijo(_ahora)).Handle(
                new TomarReporteChatCommand(reporte.Id, moderadorId), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoReporteChat.EnRevision, reporte.Estado);
        Assert.Equal(moderadorId, reporte.ModeradorAsignadoId);
        Assert.Equal(AccionModeracionChat.Tomar, Assert.Single(registros.Agregados).Accion);
        Assert.False(reporte.Tomar(Guid.NewGuid(), _ahora).EsExito);
    }

    [Fact]
    public async Task Liberar_y_resolver_exigen_moderador_asignado()
    {
        var reporte = CrearReporte();
        var asignado = Guid.NewGuid();
        reporte.Tomar(asignado, _ahora);
        var registros = new RegistrosFake();
        var reportes = new ReportesFake(reporte: reporte);

        var ajeno = await new LiberarReporteChatCommandHandler(
            reportes, registros, new RelojFijo(_ahora)).Handle(
                new LiberarReporteChatCommand(reporte.Id, Guid.NewGuid()), CancellationToken.None);
        var propio = await new LiberarReporteChatCommandHandler(
            reportes, registros, new RelojFijo(_ahora)).Handle(
                new LiberarReporteChatCommand(reporte.Id, asignado), CancellationToken.None);

        Assert.False(ajeno.EsExito);
        Assert.True(propio.EsExito);
        Assert.Equal(EstadoReporteChat.Pendiente, reporte.Estado);
        Assert.Equal(AccionModeracionChat.Liberar, Assert.Single(registros.Agregados).Accion);
    }

    [Fact]
    public async Task Atender_con_cierre_modifica_reporte_y_conversacion_atomicamente()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var reporte = ReporteChat.Crear(
            conversacion.Id, conversacion.CompradorId, TipoObjetivoReporteChat.Conversacion,
            null, CategoriaReporteChat.Acoso, null, _ahora).Valor;
        var moderador = Guid.NewGuid();
        reporte.Tomar(moderador, _ahora);
        var registros = new RegistrosFake();

        var resultado = await new AtenderReporteChatCommandHandler(
            new ReportesFake(reporte: reporte), registros, new ConversacionesFake(conversacion),
            new RelojFijo(_ahora)).Handle(
                new AtenderReporteChatCommand(reporte.Id, moderador, true), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoReporteChat.Atendido, reporte.Estado);
        Assert.Equal(EstadoConversacion.CerradaPorModeracion, conversacion.Estado);
        Assert.Equal(
            [AccionModeracionChat.CerrarConversacion, AccionModeracionChat.Atender],
            registros.Agregados.Select(x => x.Accion));
    }

    [Fact]
    public async Task Atender_ajeno_no_cierra_la_conversacion()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var reporte = ReporteChat.Crear(
            conversacion.Id, conversacion.CompradorId, TipoObjetivoReporteChat.Conversacion,
            null, CategoriaReporteChat.Acoso, null, _ahora).Valor;
        reporte.Tomar(Guid.NewGuid(), _ahora);

        var resultado = await new AtenderReporteChatCommandHandler(
            new ReportesFake(reporte: reporte), new RegistrosFake(),
            new ConversacionesFake(conversacion), new RelojFijo(_ahora)).Handle(
                new AtenderReporteChatCommand(reporte.Id, Guid.NewGuid(), true), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Dto_de_cola_no_contiene_contenido_ni_participantes()
    {
        var nombres = typeof(ReporteChatColaDto).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(nombres, n => n.Contains("Detalle", StringComparison.Ordinal));
        Assert.DoesNotContain(nombres, n => n.Contains("Reportante", StringComparison.Ordinal));
        Assert.DoesNotContain(nombres, n => n.Contains("Texto", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Evidencia_solo_se_consulta_despues_de_auditoria_persistida()
    {
        var reporte = CrearReporte();
        var consulta = new ConsultaModeracionFake();
        var resultado = await new ObtenerEvidenciaReporteChatQueryHandler(
            new ReportesFake(reporte: reporte), new AuditorFake(exito: true), consulta,
            new RelojFijo(_ahora)).Handle(
                new ObtenerEvidenciaReporteChatQuery(reporte.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.True(consulta.FueConsultada);
    }

    [Fact]
    public async Task Evidencia_no_devuelve_contenido_si_falla_auditoria()
    {
        var reporte = CrearReporte();
        var consulta = new ConsultaModeracionFake();
        var resultado = await new ObtenerEvidenciaReporteChatQueryHandler(
            new ReportesFake(reporte: reporte), new AuditorFake(exito: false), consulta,
            new RelojFijo(_ahora)).Handle(
                new ObtenerEvidenciaReporteChatQuery(reporte.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresModeracionChat.AuditoriaNoDisponible, resultado.Error.Code);
        Assert.False(consulta.FueConsultada);
    }

    private static ReporteChat CrearReporte() => ReporteChat.Crear(
        Guid.NewGuid(), Guid.NewGuid(), TipoObjetivoReporteChat.Conversacion,
        null, CategoriaReporteChat.Acoso, null, _ahora).Valor;

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

    private sealed class ReportesFake(
        bool existeAbierto = false, ReporteChat? reporte = null) : IRepositorioReportesChat
    {
        public List<ReporteChat> Agregados { get; } = [];

        public Task<ReporteChat?> ObtenerAsync(Guid reporteId, CancellationToken ct) =>
            Task.FromResult(reporte?.Id == reporteId ? reporte : null);

        public Task<bool> ExisteAbiertoAsync(
            Guid conversacionId,
            Guid reportanteId,
            TipoObjetivoReporteChat tipoObjetivo,
            Guid? mensajeId,
            CancellationToken ct) => Task.FromResult(existeAbierto);

        public void Agregar(ReporteChat reporte) => Agregados.Add(reporte);
    }

    private sealed class RegistrosFake : IRepositorioRegistrosModeracionChat
    {
        public List<RegistroModeracionChat> Agregados { get; } = [];

        public void Agregar(RegistroModeracionChat registro) => Agregados.Add(registro);
    }

    private sealed class AuditorFake(bool exito) : IAuditorModeracionChat
    {
        public Task<bool> RegistrarConsultaEvidenciaAsync(
            Guid reporteId, Guid conversacionId, Guid moderadorId, DateTimeOffset fecha,
            CancellationToken ct) => Task.FromResult(exito);
    }

    private sealed class ConsultaModeracionFake : IConsultaModeracionChat
    {
        public bool FueConsultada { get; private set; }

        public Task<IReadOnlyList<ReporteChatColaDto>> ListarAsync(
            EstadoReporteChat? estado, int limite, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReporteChatColaDto>>([]);

        public Task<EvidenciaReporteChatDto?> ObtenerEvidenciaAsync(
            Guid reporteId, CancellationToken ct)
        {
            FueConsultada = true;
            return Task.FromResult<EvidenciaReporteChatDto?>(new(
                reporteId, TipoObjetivoReporteChat.Conversacion, CategoriaReporteChat.Acoso,
                null, "Comprador", "Vendedor", []));
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
