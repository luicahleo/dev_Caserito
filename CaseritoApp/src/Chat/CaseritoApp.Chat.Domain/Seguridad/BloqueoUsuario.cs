using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Chat.Domain.Seguridad;

public sealed class BloqueoUsuario : AggregateRoot
{
    private BloqueoUsuario()
    {
    }

    private BloqueoUsuario(Guid bloqueadorId, Guid bloqueadoId, DateTimeOffset creadoEn)
    {
        BloqueadorId = bloqueadorId;
        BloqueadoId = bloqueadoId;
        CreadoEn = creadoEn;
    }

    public Guid BloqueadorId { get; private set; }

    public Guid BloqueadoId { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public static Result<BloqueoUsuario> Crear(
        Guid bloqueadorId,
        Guid bloqueadoId,
        DateTimeOffset creadoEn)
    {
        if (bloqueadorId == Guid.Empty || bloqueadoId == Guid.Empty || bloqueadorId == bloqueadoId)
        {
            return Result.Fallo<BloqueoUsuario>(new Error(
                "chat_bloqueo_invalido",
                "La solicitud no es válida."));
        }

        return Result.Exito(new BloqueoUsuario(
            bloqueadorId,
            bloqueadoId,
            creadoEn.ToUniversalTime()));
    }
}
