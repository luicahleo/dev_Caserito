using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Conversaciones;

public sealed class Conversacion : AggregateRoot
{
    private Conversacion()
    {
    }

    private Conversacion(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        DateTimeOffset creadaEn)
    {
        AvisoId = avisoId;
        CompradorId = compradorId;
        VendedorId = vendedorId;
        CreadaEn = creadaEn;
        UltimaActividadEn = creadaEn;
        Estado = EstadoConversacion.Activa;
    }

    public Guid AvisoId { get; private set; }

    public Guid CompradorId { get; private set; }

    public Guid VendedorId { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset UltimaActividadEn { get; private set; }

    public long UltimaSecuencia { get; private set; }

    public long UltimaSecuenciaLeidaComprador { get; private set; }

    public long UltimaSecuenciaLeidaVendedor { get; private set; }

    public EstadoConversacion Estado { get; private set; }

    public DateTimeOffset? CerradaEn { get; private set; }

    public Guid? UltimoActorEstadoId { get; private set; }

    public static Result<Conversacion> Crear(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        DateTimeOffset creadaEn)
    {
        if (avisoId == Guid.Empty || compradorId == Guid.Empty || vendedorId == Guid.Empty)
        {
            return Result.Fallo<Conversacion>(new Error(
                ErroresConversacion.IdentificadorInvalido,
                "Los identificadores de la conversación no son válidos."));
        }

        if (compradorId == vendedorId)
        {
            return Result.Fallo<Conversacion>(new Error(
                ErroresConversacion.ParticipantesCoinciden,
                "Los participantes de la conversación deben ser distintos."));
        }

        var fechaUtc = creadaEn.ToUniversalTime();
        var conversacion = new Conversacion(avisoId, compradorId, vendedorId, fechaUtc);
        conversacion.AgregarEvento(new ConversacionIniciada(conversacion.Id, avisoId, fechaUtc));
        return Result.Exito(conversacion);
    }

    public bool EsParticipante(Guid usuarioId) =>
        usuarioId != Guid.Empty && (usuarioId == CompradorId || usuarioId == VendedorId);

    public Result CerrarPorParticipante(Guid usuarioId, DateTimeOffset ocurrioEn)
    {
        if (!EsParticipante(usuarioId))
        {
            return NoEncontrada();
        }

        if (Estado == EstadoConversacion.CerradaPorModeracion)
        {
            return NoDisponible();
        }

        return Estado == EstadoConversacion.Cerrada
            ? Result.Exito()
            : CambiarEstado(EstadoConversacion.Cerrada, usuarioId, ocurrioEn);
    }

    public Result ReabrirPorParticipante(Guid usuarioId, DateTimeOffset ocurrioEn)
    {
        if (!EsParticipante(usuarioId))
        {
            return NoEncontrada();
        }

        if (Estado == EstadoConversacion.CerradaPorModeracion)
        {
            return NoDisponible();
        }

        return Estado == EstadoConversacion.Activa
            ? Result.Exito()
            : CambiarEstado(EstadoConversacion.Activa, usuarioId, ocurrioEn);
    }

    public Result CerrarPorModeracion(Guid moderadorId, DateTimeOffset ocurrioEn)
    {
        if (moderadorId == Guid.Empty)
        {
            return Result.Fallo(new Error(
                ErroresConversacion.IdentificadorInvalido,
                "La solicitud no es válida."));
        }

        return Estado == EstadoConversacion.CerradaPorModeracion
            ? Result.Exito()
            : CambiarEstado(EstadoConversacion.CerradaPorModeracion, moderadorId, ocurrioEn);
    }

    public Result ReabrirPorModeracion(Guid moderadorId, DateTimeOffset ocurrioEn)
    {
        if (moderadorId == Guid.Empty)
        {
            return Result.Fallo(new Error(
                ErroresConversacion.IdentificadorInvalido,
                "La solicitud no es válida."));
        }

        return Estado == EstadoConversacion.Activa
            ? Result.Exito()
            : CambiarEstado(EstadoConversacion.Activa, moderadorId, ocurrioEn);
    }

    public Result<Mensaje> CrearMensaje(
        Guid remitenteId,
        Guid claveIdempotencia,
        long secuencia,
        string texto,
        DateTimeOffset enviadoEn)
    {
        if (!EsParticipante(remitenteId))
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        if (Estado != EstadoConversacion.Activa)
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.NoDisponibleParaEnvio,
                "La conversación no está disponible para enviar mensajes."));
        }

        if (claveIdempotencia == Guid.Empty)
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.IdentificadorInvalido,
                "La solicitud de mensaje no es válida."));
        }

        if (secuencia <= UltimaSecuencia)
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.SecuenciaInvalida,
                "La secuencia del mensaje no es válida."));
        }

        var textoNormalizado = texto?.Trim();
        if (string.IsNullOrWhiteSpace(textoNormalizado) || textoNormalizado.Length > 2000)
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.TextoInvalido,
                "El mensaje debe tener entre 1 y 2000 caracteres."));
        }

        var fechaUtc = enviadoEn.ToUniversalTime();
        var mensaje = new Mensaje(
            Id,
            remitenteId,
            claveIdempotencia,
            secuencia,
            textoNormalizado,
            fechaUtc);

        UltimaSecuencia = secuencia;
        if (fechaUtc > UltimaActividadEn)
        {
            UltimaActividadEn = fechaUtc;
        }

        AgregarEvento(new MensajeEnviado(Id, mensaje.Id, secuencia, fechaUtc));
        return Result.Exito(mensaje);
    }

    public Result MarcarLectura(Guid usuarioId, long hastaSecuencia, DateTimeOffset ocurrioEn)
    {
        if (!EsParticipante(usuarioId))
        {
            return Result.Fallo(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        if (hastaSecuencia < 0 || hastaSecuencia > UltimaSecuencia)
        {
            return Result.Fallo(new Error(
                ErroresConversacion.SecuenciaInvalida,
                "La secuencia de lectura no es válida."));
        }

        var secuenciaActual = usuarioId == CompradorId
            ? UltimaSecuenciaLeidaComprador
            : UltimaSecuenciaLeidaVendedor;
        if (hastaSecuencia <= secuenciaActual)
        {
            return Result.Exito();
        }

        if (usuarioId == CompradorId)
        {
            UltimaSecuenciaLeidaComprador = hastaSecuencia;
        }
        else
        {
            UltimaSecuenciaLeidaVendedor = hastaSecuencia;
        }

        AgregarEvento(new LecturaAvanzada(Id, hastaSecuencia, ocurrioEn.ToUniversalTime()));
        return Result.Exito();
    }

    private Result CambiarEstado(
        EstadoConversacion estado,
        Guid actorId,
        DateTimeOffset ocurrioEn)
    {
        var fechaUtc = ocurrioEn.ToUniversalTime();
        Estado = estado;
        CerradaEn = estado == EstadoConversacion.Activa ? null : fechaUtc;
        UltimoActorEstadoId = actorId;
        AgregarEvento(new ConversacionEstadoCambiado(Id, estado, fechaUtc));
        return Result.Exito();
    }

    private static Result NoEncontrada() => Result.Fallo(new Error(
        ErroresConversacion.NoEncontrada,
        "La conversación no está disponible."));

    private static Result NoDisponible() => Result.Fallo(new Error(
        ErroresConversacion.NoDisponibleParaEnvio,
        "La conversación no está disponible para enviar mensajes."));
}
