namespace CaseritoApp.Identity.Application.Correo;

public interface IGeneradorTokenEmail
{
    public string Generar(Guid usuarioId);
    public bool Validar(string token, out Guid usuarioId);
}
