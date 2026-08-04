using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.IntegrationTests;

public sealed class BootstrapUsuariosPruebaTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Development_habilitado_crea_cuentas_con_roles_exactos_y_vendedor_sin_kyc()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var consultaKyc = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();
        var sufijo = Guid.NewGuid().ToString("N");
        var opciones = OpcionesValidas(sufijo);
        var sut = new BootstrapUsuariosPrueba(usuarios, new LoggerCaptura<BootstrapUsuariosPrueba>());

        await sut.EjecutarAsync(opciones, esDevelopment: true);

        await AssertRolesAsync(usuarios, opciones.Administrador.Email, RolesApp.AdminPlataforma);
        await AssertRolesAsync(usuarios, opciones.Vendedor.Email, RolesApp.Cliente);
        await AssertRolesAsync(usuarios, opciones.Comprador.Email, RolesApp.Cliente);
        var vendedor = await usuarios.FindByEmailAsync(opciones.Vendedor.Email);
        Assert.NotNull(vendedor);
        Assert.False(await consultaKyc.EstaVerificadoAsync(vendedor.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task No_crea_cuentas_fuera_de_Development_o_sin_habilitacion(
        bool esDevelopment,
        bool habilitado)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var opciones = OpcionesValidas(Guid.NewGuid().ToString("N")) with { Habilitado = habilitado };
        var sut = new BootstrapUsuariosPrueba(usuarios, new LoggerCaptura<BootstrapUsuariosPrueba>());

        await sut.EjecutarAsync(opciones, esDevelopment);

        Assert.Null(await usuarios.FindByEmailAsync(opciones.Administrador.Email));
        Assert.Null(await usuarios.FindByEmailAsync(opciones.Vendedor.Email));
        Assert.Null(await usuarios.FindByEmailAsync(opciones.Comprador.Email));
    }

    [Fact]
    public async Task Configuracion_incompleta_omite_todo_el_bootstrap()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var opciones = OpcionesValidas(Guid.NewGuid().ToString("N"));
        opciones = opciones with
        {
            Comprador = opciones.Comprador with { Password = string.Empty },
        };
        var sut = new BootstrapUsuariosPrueba(usuarios, new LoggerCaptura<BootstrapUsuariosPrueba>());

        await sut.EjecutarAsync(opciones, esDevelopment: true);

        Assert.Null(await usuarios.FindByEmailAsync(opciones.Administrador.Email));
        Assert.Null(await usuarios.FindByEmailAsync(opciones.Vendedor.Email));
    }

    [Fact]
    public async Task Reejecucion_no_modifica_usuarios_existentes_ni_registra_configuracion()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var sufijo = Guid.NewGuid().ToString("N");
        var opciones = OpcionesValidas(sufijo);
        var existente = new ApplicationUser
        {
            UserName = opciones.Vendedor.Email,
            Email = opciones.Vendedor.Email,
            Nombres = "Nombre previo",
            CiudadId = new Guid("22222222-2222-2222-2222-000000000001"),
        };
        const string clavePrevia = "Clave-Previa-987!";
        Assert.True((await usuarios.CreateAsync(existente, clavePrevia)).Succeeded);
        Assert.True((await usuarios.AddToRoleAsync(existente, RolesApp.Moderador)).Succeeded);
        var logger = new LoggerCaptura<BootstrapUsuariosPrueba>();
        var sut = new BootstrapUsuariosPrueba(usuarios, logger);

        await sut.EjecutarAsync(opciones, esDevelopment: true);
        await sut.EjecutarAsync(opciones, esDevelopment: true);

        var despues = await usuarios.FindByEmailAsync(opciones.Vendedor.Email);
        Assert.NotNull(despues);
        Assert.Equal("Nombre previo", despues.Nombres);
        Assert.Equal(new Guid("22222222-2222-2222-2222-000000000001"), despues.CiudadId);
        Assert.True(await usuarios.CheckPasswordAsync(despues, clavePrevia));
        Assert.Equal([RolesApp.Moderador], await usuarios.GetRolesAsync(despues));
        var textoLogs = string.Join(Environment.NewLine, logger.Mensajes);
        foreach (var valor in ValoresSensibles(opciones))
        {
            Assert.DoesNotContain(valor, textoLogs, StringComparison.Ordinal);
        }
    }

    private static OpcionesBootstrapUsuariosPrueba OpcionesValidas(string sufijo) => new(
        Habilitado: true,
        Administrador: new CuentaBootstrapPrueba(
            $"admin-{sufijo}@example.invalid", "Clave-Admin-123!", "Admin sintético", "Cochabamba"),
        Vendedor: new CuentaBootstrapPrueba(
            $"vendedor-{sufijo}@example.invalid", "Clave-Vendedor-123!", "Vendedor sintético", "La Paz"),
        Comprador: new CuentaBootstrapPrueba(
            $"comprador-{sufijo}@example.invalid", "Clave-Comprador-123!", "Comprador sintético", "Sucre"));

    private static IEnumerable<string> ValoresSensibles(OpcionesBootstrapUsuariosPrueba opciones) =>
    [
        opciones.Administrador.Email,
        opciones.Administrador.Password,
        opciones.Administrador.Nombre,
        opciones.Administrador.Ciudad,
        opciones.Vendedor.Email,
        opciones.Vendedor.Password,
        opciones.Vendedor.Nombre,
        opciones.Vendedor.Ciudad,
        opciones.Comprador.Email,
        opciones.Comprador.Password,
        opciones.Comprador.Nombre,
        opciones.Comprador.Ciudad,
    ];

    private static async Task AssertRolesAsync(
        UserManager<ApplicationUser> usuarios,
        string email,
        params string[] esperados)
    {
        var usuario = await usuarios.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.Equal(esperados, await usuarios.GetRolesAsync(usuario));
    }

    private sealed class LoggerCaptura<T> : ILogger<T>
    {
        public List<string> Mensajes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Mensajes.Add(formatter(state, exception));
    }
}
