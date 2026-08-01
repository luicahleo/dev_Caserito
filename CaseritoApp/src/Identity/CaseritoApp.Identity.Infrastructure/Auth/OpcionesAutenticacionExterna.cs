namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Credenciales configurables para los proveedores de autenticación externa.</summary>
public sealed class OpcionesAutenticacionExterna
{
    public const string Seccion = "Authentication";

    public OpcionesGoogle Google { get; init; } = new();

    public OpcionesFacebook Facebook { get; init; } = new();

    public sealed class OpcionesGoogle
    {
        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;

        public bool Habilitado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    public sealed class OpcionesFacebook
    {
        public string AppId { get; init; } = string.Empty;

        public string AppSecret { get; init; } = string.Empty;

        public bool Habilitado => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
    }
}
