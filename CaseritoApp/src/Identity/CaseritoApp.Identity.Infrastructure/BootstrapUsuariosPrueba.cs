using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Datos sintéticos de una cuenta local preparada exclusivamente para pruebas manuales.</summary>
public sealed record CuentaBootstrapPrueba(
    string Email,
    string Password,
    string Nombre,
    string Ciudad)
{
    /// <summary>Cuenta vacía usada cuando la sección no está configurada.</summary>
    public CuentaBootstrapPrueba() : this(string.Empty, string.Empty, string.Empty, string.Empty)
    {
    }

    internal bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(Nombre)
        && !string.IsNullOrWhiteSpace(Ciudad);
}

/// <summary>Configuración opt-in del bootstrap local de las tres cuentas mínimas.</summary>
public sealed record OpcionesBootstrapUsuariosPrueba(
    bool Habilitado,
    CuentaBootstrapPrueba Administrador,
    CuentaBootstrapPrueba Vendedor,
    CuentaBootstrapPrueba Comprador)
{
    /// <summary>Configuración segura por defecto: deshabilitada y sin datos.</summary>
    public OpcionesBootstrapUsuariosPrueba()
        : this(false, new CuentaBootstrapPrueba(), new CuentaBootstrapPrueba(), new CuentaBootstrapPrueba())
    {
    }

    internal bool EstaCompleta =>
        Administrador.EstaCompleta && Vendedor.EstaCompleta && Comprador.EstaCompleta;
}

/// <summary>
/// Crea cuentas sintéticas mínimas sin modificar nunca un usuario que ya exista.
/// </summary>
public sealed partial class BootstrapUsuariosPrueba(
    UserManager<ApplicationUser> usuarios,
    ILogger<BootstrapUsuariosPrueba> logger)
{
    /// <summary>Ejecuta el bootstrap únicamente cuando todos los guardrails están satisfechos.</summary>
    public async Task EjecutarAsync(
        OpcionesBootstrapUsuariosPrueba opciones,
        bool esDevelopment,
        CancellationToken cancellationToken = default)
    {
        if (!esDevelopment || !opciones.Habilitado || !opciones.EstaCompleta)
        {
            BootstrapOmitido(logger);
            return;
        }

        var creados = 0;
        creados += await CrearSiNoExisteAsync(opciones.Administrador, RolesApp.AdminPlataforma);
        creados += await CrearSiNoExisteAsync(opciones.Vendedor, RolesApp.Cliente);
        creados += await CrearSiNoExisteAsync(opciones.Comprador, RolesApp.Cliente);
        BootstrapCompletado(logger, creados);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<int> CrearSiNoExisteAsync(CuentaBootstrapPrueba cuenta, string rol)
    {
        if (await usuarios.FindByEmailAsync(cuenta.Email) is not null)
        {
            return 0;
        }

        var usuario = new ApplicationUser
        {
            UserName = cuenta.Email,
            Email = cuenta.Email,
            Nombre = cuenta.Nombre,
            Ciudad = cuenta.Ciudad,
        };

        if (!(await usuarios.CreateAsync(usuario, cuenta.Password)).Succeeded)
        {
            throw new InvalidOperationException("No se pudo preparar el entorno de pruebas.");
        }

        if (!(await usuarios.AddToRoleAsync(usuario, rol)).Succeeded)
        {
            throw new InvalidOperationException("No se pudo preparar el entorno de pruebas.");
        }

        return 1;
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Debug,
        Message = "Bootstrap de usuarios de prueba omitido.")]
    private static partial void BootstrapOmitido(ILogger logger);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Bootstrap de usuarios de prueba completado. Cuentas nuevas: {Cantidad}.")]
    private static partial void BootstrapCompletado(ILogger logger, int cantidad);
}
