namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Identidad externa temporal, protegida hasta completar o vincular el acceso.</summary>
public sealed record LoginExternoPendiente(
    string Proveedor,
    string ClaveProveedor,
    string? Email,
    bool EmailConfiable,
    string? Nombre,
    string Retorno,
    DateTimeOffset ExpiraEn);

/// <summary>Vista segura de los datos que aún debe aportar o resolver la PWA.</summary>
public sealed record LoginExternoPendienteProyeccion(
    bool RequiereEmail,
    bool RequiereNombre,
    bool RequiereCiudad,
    bool RequiereVinculacion,
    string? NombreVisible);
