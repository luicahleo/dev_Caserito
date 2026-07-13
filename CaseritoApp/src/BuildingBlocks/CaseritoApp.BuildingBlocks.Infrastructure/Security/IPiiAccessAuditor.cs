namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Registra en un log append-only cada acceso a PII sensible.</summary>
public interface IPiiAccessAuditor
{
    public Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct);
}
