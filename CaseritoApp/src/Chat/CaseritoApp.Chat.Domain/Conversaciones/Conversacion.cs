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
    }

    public Guid AvisoId { get; private set; }

    public Guid CompradorId { get; private set; }

    public Guid VendedorId { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset UltimaActividadEn { get; private set; }

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
}
