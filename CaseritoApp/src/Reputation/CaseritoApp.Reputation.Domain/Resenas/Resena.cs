using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Reputation.Domain.Resenas;

public sealed class Resena : AggregateRoot
{
    private Resena()
    {
        Comentario = null!;
        Version = null!;
    }

    private Resena(
        Guid orderId,
        Guid autorId,
        Guid destinatarioId,
        RolAutorResena rolAutor,
        int puntuacion,
        string comentario,
        DateTimeOffset creadaEn)
    {
        OrderId = orderId;
        AutorId = autorId;
        DestinatarioId = destinatarioId;
        RolAutor = rolAutor;
        Puntuacion = puntuacion;
        Comentario = comentario;
        CreadaEn = creadaEn;
        Version = [];
    }

    public Guid OrderId { get; private set; }

    public Guid AutorId { get; private set; }

    public Guid DestinatarioId { get; private set; }

    public RolAutorResena RolAutor { get; private set; }

    public int Puntuacion { get; private set; }

    public string Comentario { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public byte[] Version { get; private set; }

    public static Result<Resena> Crear(
        Guid orderId,
        Guid autorId,
        Guid destinatarioId,
        RolAutorResena rolAutor,
        int puntuacion,
        string comentario,
        DateTimeOffset ocurrioEn)
    {
        var comentarioNormalizado = comentario?.Trim();
        if (orderId == Guid.Empty
            || autorId == Guid.Empty
            || destinatarioId == Guid.Empty
            || autorId == destinatarioId
            || rolAutor is not (RolAutorResena.Comprador or RolAutorResena.Vendedor)
            || puntuacion is < 1 or > 5
            || comentarioNormalizado is null
            || comentarioNormalizado.Length is < 10 or > 500)
        {
            return Result.Fallo<Resena>(new Error(
                ErroresResena.Invalida,
                "La reseña no es válida."));
        }

        return Result.Exito(new Resena(
            orderId,
            autorId,
            destinatarioId,
            rolAutor,
            puntuacion,
            comentarioNormalizado,
            ocurrioEn.ToUniversalTime()));
    }
}
