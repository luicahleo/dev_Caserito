namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Permisos atómicos del MVP. El código de autorización chequea permisos, no roles.</summary>
public static class Permisos
{
    /// <summary>Ocultar/eliminar publicaciones reportadas.</summary>
    public const string PublicacionesModerar = "publicaciones.moderar";

    /// <summary>Moderar conversaciones de chat reportadas.</summary>
    public const string ChatModerar = "chat.moderar";

    /// <summary>Aprobar/rechazar verificaciones de identidad.</summary>
    public const string KycRevisar = "kyc.revisar";

    /// <summary>Gestionar usuarios (roles, suspensión).</summary>
    public const string UsuariosGestionar = "usuarios.gestionar";

    /// <summary>Atender tickets de soporte (futuro Disputes).</summary>
    public const string SoporteTickets = "soporte.tickets";

    /// <summary>Todos los permisos definidos.</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        PublicacionesModerar, ChatModerar, KycRevisar, UsuariosGestionar, SoporteTickets,
    ];
}
