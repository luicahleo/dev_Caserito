using CaseritoApp.Identity.Application.Correo;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed class PlantillaCorreoTextoPlano : IPlantillaCorreo
{
    public string AsuntoConfirmacionEmail(string nombre) =>
        "Confirma tu cuenta en Caserito";

    public string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion) =>
        $"Hola {nombre},\n\nPara activar tu cuenta, haz clic en el siguiente enlace:\n{urlConfirmacion}\n\n" +
        "Si no creaste esta cuenta, ignora este correo.\n\nEquipo Caserito";

    public string AsuntoKycAprobado(string nombre) =>
        "Tu identidad ha sido verificada";

    public string CuerpoKycAprobado(string nombre) =>
        $"Hola {nombre},\n\nTu verificación de identidad fue aprobada. Ya puedes publicar y vender en Caserito.\n\nEquipo Caserito";

    public string AsuntoKycRechazado(string nombre) =>
        "Tu verificación de identidad fue rechazada";

    public string CuerpoKycRechazado(string nombre, string motivo) =>
        $"Hola {nombre},\n\nTu verificación de identidad no pudo ser aprobada.\n\nMotivo: {motivo}\n\n" +
        "Puedes volver a intentarlo desde tu perfil.\n\nEquipo Caserito";

    public string AsuntoRestablecimientoPassword(string nombre) =>
        "Restablece tu contraseña de Caserito";

    public string CuerpoRestablecimientoPassword(string nombre, string urlRestablecimiento) =>
        $"Hola {nombre},\n\nRecibimos una solicitud para restablecer tu contraseña. " +
        $"Este enlace será válido durante 30 minutos:\n{urlRestablecimiento}\n\n" +
        "Si no solicitaste este cambio, ignora este correo.\n\nEquipo Caserito";

    // Aviso interno: no lleva nombre, CI, score, motivo ni identificadores. Solo dice que hay
    // trabajo pendiente y a dónde ir; los datos se ven en el panel, que audita el acceso.
    public string AsuntoSolicitudKycEnEspera() =>
        "Hay una solicitud de verificación esperando revisión";

    public string CuerpoSolicitudKycEnEspera(string urlPanel) =>
        "Una solicitud de verificación de identidad quedó pendiente de revisión manual.\n\n" +
        $"Revísala en el panel de administración:\n{urlPanel}\n\nEquipo Caserito";
}
