namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Registra en un log append-only cada acceso a PII sensible (sin exponer la PII misma).</summary>
public interface IAuditorAccesoPii
{
    public Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct);
}
