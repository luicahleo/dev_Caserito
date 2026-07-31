namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    public string Host { get; set; } = "mail";
    public int Puerto { get; set; } = 587;
    public string Remitente { get; set; } = "noreply@trajano.online";
    public string NombreRemitente { get; set; } = "Caserito";
    public bool HabilitarSsl { get; set; }
}
