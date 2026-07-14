namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Refresh token persistido hasheado (nunca en claro). Se rota en cada uso.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public string? ReemplazadoPorHash { get; set; }

    public bool EsActivo(DateTimeOffset ahora) => RevocadoEn is null && ExpiraEn > ahora;
}
