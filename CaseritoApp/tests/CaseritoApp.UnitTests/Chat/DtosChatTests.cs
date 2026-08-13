using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.UnitTests.Chat;

public sealed class DtosChatTests
{
    private static readonly DateTimeOffset _ahora = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConversacionDto_mapea_recibos_de_la_contraparte_segun_participante()
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, _ahora).Valor;
        conversacion.CrearMensaje(vendedorId, Guid.NewGuid(), 3, "Hola", _ahora);
        conversacion.MarcarLectura(compradorId, 3, _ahora.AddMinutes(1));
        conversacion.CrearMensaje(compradorId, Guid.NewGuid(), 5, "Buenas", _ahora.AddMinutes(2));
        conversacion.MarcarLectura(vendedorId, 5, _ahora.AddMinutes(3));

        var paraComprador = ConversacionDto.Desde(conversacion, true, compradorId);
        var paraVendedor = ConversacionDto.Desde(conversacion, true, vendedorId);
        var sinParticipante = ConversacionDto.Desde(conversacion);

        Assert.Equal(5, paraComprador.UltimaSecuenciaEntregadaContraparte);
        Assert.Equal(5, paraComprador.UltimaSecuenciaLeidaContraparte);
        Assert.Equal(3, paraVendedor.UltimaSecuenciaEntregadaContraparte);
        Assert.Equal(3, paraVendedor.UltimaSecuenciaLeidaContraparte);
        Assert.Equal(0, sinParticipante.UltimaSecuenciaEntregadaContraparte);
        Assert.Equal(conversacion.Id, sinParticipante.Id);
        Assert.Equal(conversacion.AvisoId, sinParticipante.AvisoId);
        Assert.Equal(compradorId, sinParticipante.CompradorId);
        Assert.Equal(vendedorId, sinParticipante.VendedorId);
        Assert.Equal(_ahora, sinParticipante.CreadaEn);
        Assert.Equal(conversacion.UltimaActividadEn, sinParticipante.UltimaActividadEn);
        Assert.Equal(5, sinParticipante.UltimaSecuencia);
        Assert.Equal(EstadoConversacion.Activa, sinParticipante.Estado);
        Assert.Null(sinParticipante.OrigenCierre);
        Assert.True(sinParticipante.PuedeEnviar);
        Assert.Equal(0, sinParticipante.UltimaSecuenciaLeidaContraparte);
    }

    [Fact]
    public void Dtos_de_consulta_conservan_campos_del_contrato()
    {
        var conversacionId = Guid.NewGuid();
        var resumen = new ConversacionResumenDto(
            conversacionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Vendedor",
            _ahora,
            _ahora.AddMinutes(1),
            9,
            2,
            EstadoConversacion.Activa,
            null,
            true,
            8,
            7);
        var reporte = new ReporteChatColaDto(
            Guid.NewGuid(),
            conversacionId,
            TipoObjetivoReporteChat.Conversacion,
            null,
            CategoriaReporteChat.Spam,
            EstadoReporteChat.Pendiente,
            _ahora,
            null,
            null);
        var mensaje = new MensajeEvidenciaChatDto(
            Guid.NewGuid(), 9, "Vendedor", "Texto", _ahora, true);
        var evidencia = new EvidenciaReporteChatDto(
            reporte.Id,
            reporte.TipoObjetivo,
            reporte.Categoria,
            "Detalle",
            "Comprador",
            "Vendedor",
            [mensaje]);

        Assert.Equal(conversacionId, resumen.Id);
        Assert.NotEqual(Guid.Empty, resumen.AvisoId);
        Assert.NotEqual(Guid.Empty, resumen.ContraparteId);
        Assert.Equal("Vendedor", resumen.Rol);
        Assert.Equal(_ahora, resumen.CreadaEn);
        Assert.Equal(_ahora.AddMinutes(1), resumen.UltimaActividadEn);
        Assert.Equal(9, resumen.UltimaSecuencia);
        Assert.Equal(2, resumen.NoLeidos);
        Assert.Equal(EstadoConversacion.Activa, resumen.Estado);
        Assert.Null(resumen.OrigenCierre);
        Assert.True(resumen.PuedeEnviar);
        Assert.Equal(8, resumen.UltimaSecuenciaEntregadaContraparte);
        Assert.Equal(7, resumen.UltimaSecuenciaLeidaContraparte);
        Assert.NotEqual(Guid.Empty, reporte.Id);
        Assert.Equal(conversacionId, reporte.ConversacionId);
        Assert.Equal(TipoObjetivoReporteChat.Conversacion, reporte.TipoObjetivo);
        Assert.Null(reporte.MensajeId);
        Assert.Equal(CategoriaReporteChat.Spam, reporte.Categoria);
        Assert.Equal(EstadoReporteChat.Pendiente, reporte.Estado);
        Assert.Equal(_ahora, reporte.CreadoEn);
        Assert.Null(reporte.TomadoEn);
        Assert.Null(reporte.ResueltoEn);
        Assert.Equal(reporte.Id, evidencia.ReporteId);
        Assert.Equal("Detalle", evidencia.Detalle);
        Assert.Equal("Comprador", evidencia.RolReportante);
        Assert.Equal("Vendedor", evidencia.RolObjetivo);
        Assert.Same(mensaje, Assert.Single(evidencia.Mensajes));
        Assert.NotEqual(Guid.Empty, mensaje.Id);
        Assert.Equal(9, mensaje.Secuencia);
        Assert.Equal("Vendedor", mensaje.AutorRol);
        Assert.Equal("Texto", mensaje.Texto);
        Assert.Equal(_ahora, mensaje.EnviadoEn);
        Assert.True(mensaje.EsObjetivo);

        var frontera = new FronteraConversaciones(_ahora, conversacionId);
        Assert.Equal(_ahora, frontera.UltimaActividadEn);
        Assert.Equal(conversacionId, frontera.ConversacionId);
    }

    [Fact]
    public void MensajeDto_mapea_mensaje_y_resultado_de_envio()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _ahora).Valor;
        var mensaje = conversacion.CrearMensaje(
            conversacion.CompradorId, Guid.NewGuid(), 1, "Hola", _ahora).Valor;

        var dto = MensajeDto.Desde(mensaje);
        var resultado = new EnviarMensajeResultadoDto(dto, conversacion.VendedorId, true);

        Assert.Equal(mensaje.Id, dto.Id);
        Assert.Equal(mensaje.ConversacionId, dto.ConversacionId);
        Assert.Equal(mensaje.RemitenteId, dto.RemitenteId);
        Assert.Equal(1, dto.Secuencia);
        Assert.Equal("Hola", dto.Texto);
        Assert.Equal(_ahora, dto.EnviadoEn);
        Assert.Same(dto, resultado.Mensaje);
        Assert.Equal(conversacion.VendedorId, resultado.DestinatarioId);
        Assert.True(resultado.FueCreado);
    }

    [Fact]
    public void ReportarChatValidator_exige_ids_y_coherencia_del_objetivo()
    {
        var sinMensaje = new ReportarChatCommandValidator().Validate(new ReportarChatCommand(
            Guid.Empty,
            Guid.Empty,
            TipoObjetivoReporteChat.Mensaje,
            null,
            CategoriaReporteChat.Spam,
            new string('x', 1001)));
        var mensajeInesperado = new ReportarChatCommandValidator().Validate(new ReportarChatCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoObjetivoReporteChat.Conversacion,
            Guid.NewGuid(),
            CategoriaReporteChat.Spam,
            null));

        Assert.False(sinMensaje.IsValid);
        Assert.Contains(sinMensaje.Errors, x => x.PropertyName == nameof(ReportarChatCommand.MensajeId));
        Assert.Contains(sinMensaje.Errors, x => x.PropertyName == nameof(ReportarChatCommand.Detalle));
        Assert.False(mensajeInesperado.IsValid);
    }
}
