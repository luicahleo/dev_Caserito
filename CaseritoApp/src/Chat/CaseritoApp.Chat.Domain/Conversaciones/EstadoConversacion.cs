namespace CaseritoApp.Chat.Domain.Conversaciones;

public enum EstadoConversacion
{
    Activa,
    Cerrada,
    CerradaPorModeracion,

    // Debe permanecer en último lugar: EF mapea este enum como int por convención
    // y reordenar los valores cambiaría el significado de las filas existentes.
    RetenidaPorVerificacion,
}
