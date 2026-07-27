namespace CaseritoApp.Notifications.Application.PuntosEncuentro;

public sealed record PuntoEncuentroSeguroDto(
    Guid Id,
    string Nombre,
    string Ciudad,
    string Direccion,
    bool Activo);
