namespace CaseritoApp.Identity.Application.Correo;

public sealed record MensajeCorreo(
    string Para,
    string Asunto,
    string CuerpoTexto,
    string? CuerpoHtml = null);
