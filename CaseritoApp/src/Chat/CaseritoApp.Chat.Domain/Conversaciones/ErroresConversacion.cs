namespace CaseritoApp.Chat.Domain.Conversaciones;

public static class ErroresConversacion
{
    public const string IdentificadorInvalido = "chat_identificador_invalido";
    public const string ParticipantesCoinciden = "chat_participantes_coinciden";
    public const string NoEncontrada = "chat_conversacion_no_encontrada";
    public const string TextoInvalido = "chat_texto_invalido";
    public const string SecuenciaInvalida = "chat_secuencia_invalida";
    public const string AvisoNoContactable = "chat_aviso_no_contactable";
    public const string ClaveIdempotenciaReutilizada = "chat_clave_idempotencia_reutilizada";
}
