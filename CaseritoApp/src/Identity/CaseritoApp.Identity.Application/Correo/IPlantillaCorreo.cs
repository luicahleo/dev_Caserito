namespace CaseritoApp.Identity.Application.Correo;

public interface IPlantillaCorreo
{
    public string AsuntoConfirmacionEmail(string nombre);
    public string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion);
    public string AsuntoKycAprobado(string nombre);
    public string CuerpoKycAprobado(string nombre);
    public string AsuntoKycRechazado(string nombre);
    public string CuerpoKycRechazado(string nombre, string motivo);
}
