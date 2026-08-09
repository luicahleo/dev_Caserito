using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Domain.Push;

public sealed class SuscripcionPush : AggregateRoot
{
    private static readonly Error _invalida = new(
        "Notifications.Push.Invalida", "La suscripción no es válida.");

    private SuscripcionPush()
    {
    }

    private SuscripcionPush(
        Guid usuarioId,
        string dispositivoId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset ahora)
    {
        UsuarioId = usuarioId;
        DispositivoId = dispositivoId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        CreadaEn = ahora.ToUniversalTime();
        ActualizadaEn = CreadaEn;
        Activa = true;
    }

    public Guid UsuarioId { get; private set; }
    public string DispositivoId { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }
    public DateTimeOffset? RevocadaEn { get; private set; }
    public bool Activa { get; private set; }

    public static Result<SuscripcionPush> Crear(
        Guid usuarioId,
        string dispositivoId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset ahora)
    {
        if (!DatosValidos(usuarioId, dispositivoId, endpoint, p256dh, auth))
        {
            return Result.Fallo<SuscripcionPush>(_invalida);
        }

        return Result.Exito(new SuscripcionPush(
            usuarioId, dispositivoId, endpoint, p256dh, auth, ahora));
    }

    public Result Actualizar(
        Guid usuarioId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset ahora)
    {
        if (usuarioId != UsuarioId
            || !DatosValidos(usuarioId, DispositivoId, endpoint, p256dh, auth))
        {
            return Result.Fallo(_invalida);
        }

        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        ActualizadaEn = ahora.ToUniversalTime();
        RevocadaEn = null;
        Activa = true;
        return Result.Exito();
    }

    public Result Revocar(Guid usuarioId, DateTimeOffset ahora)
    {
        if (usuarioId != UsuarioId)
        {
            return Result.Fallo(_invalida);
        }

        if (Activa)
        {
            Activa = false;
            RevocadaEn = ahora.ToUniversalTime();
            ActualizadaEn = RevocadaEn.Value;
        }

        return Result.Exito();
    }

    public void Desactivar(DateTimeOffset ahora)
    {
        Activa = false;
        RevocadaEn = ahora.ToUniversalTime();
        ActualizadaEn = RevocadaEn.Value;
    }

    private static bool DatosValidos(
        Guid usuarioId, string dispositivoId, string endpoint, string p256dh, string auth) =>
        usuarioId != Guid.Empty
        && !string.IsNullOrWhiteSpace(dispositivoId) && dispositivoId.Length <= 128
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && endpoint.Length <= 2048
        && !string.IsNullOrWhiteSpace(p256dh) && p256dh.Length <= 512
        && !string.IsNullOrWhiteSpace(auth) && auth.Length <= 256;
}
