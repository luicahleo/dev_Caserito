namespace CaseritoApp.Identity.Application.Correo;

public interface IServicioCorreo
{
    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}
