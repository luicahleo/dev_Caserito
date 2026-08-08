using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.IntegrationTests;

public sealed class SeederUsuariosDesarrolloTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Es_opt_in_crea_matriz_completa_repara_estados_y_no_registra_secretos()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var consultaKyc = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();
        var opciones = OpcionesValidas();
        var logger = new LoggerCaptura<SeederUsuariosDesarrollo>();
        var sut = new SeederUsuariosDesarrollo(usuarios, db, logger);

        await sut.EjecutarAsync(opciones, esDevelopment: false);
        await sut.EjecutarAsync(opciones with { Habilitado = false }, esDevelopment: true);
        await sut.EjecutarAsync(opciones with { Password = string.Empty }, esDevelopment: true);
        Assert.Null(await usuarios.FindByEmailAsync(opciones.Correos[SeederUsuariosDesarrollo.AliasAdminPlataforma]));

        await sut.EjecutarAsync(opciones, esDevelopment: true);

        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasAdminPlataforma, RolesApp.AdminPlataforma, true, false);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasRevisorKyc, RolesApp.AdminKyc, true, false);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasModerador, RolesApp.Moderador, true, false);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasSoporte, RolesApp.Soporte, true, false);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasVendedor1, RolesApp.Cliente, true, true);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasVendedor2, RolesApp.Cliente, true, true);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasComprador1, RolesApp.Cliente, true, true);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasComprador2, RolesApp.Cliente, true, true);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasEmailPendiente, RolesApp.Cliente, false, false);
        await AssertCuentaAsync(usuarios, consultaKyc, opciones, SeederUsuariosDesarrollo.AliasKycNoIniciado, RolesApp.Cliente, true, false);

        var pendiente = await usuarios.FindByEmailAsync(opciones.Correos[SeederUsuariosDesarrollo.AliasKycPendiente]);
        var rechazado = await usuarios.FindByEmailAsync(opciones.Correos[SeederUsuariosDesarrollo.AliasKycRechazado]);
        Assert.NotNull(pendiente);
        Assert.NotNull(rechazado);
        Assert.Equal(EstadoKyc.Pendiente, await EstadoActualAsync(db, pendiente.Id));
        Assert.False(await consultaKyc.EstaVerificadoAsync(pendiente.Id, CancellationToken.None));
        Assert.Equal(EstadoKyc.Rechazada, await EstadoActualAsync(db, rechazado.Id));
        Assert.False(await consultaKyc.EstaVerificadoAsync(rechazado.Id, CancellationToken.None));

        pendiente.EmailConfirmed = false;
        Assert.True((await usuarios.UpdateAsync(pendiente)).Succeeded);
        Assert.True((await usuarios.AddToRoleAsync(pendiente, RolesApp.Moderador)).Succeeded);

        await sut.EjecutarAsync(opciones, esDevelopment: true);

        pendiente = await usuarios.FindByEmailAsync(opciones.Correos[SeederUsuariosDesarrollo.AliasKycPendiente]);
        Assert.NotNull(pendiente);
        Assert.True(pendiente.EmailConfirmed);
        Assert.Equal([RolesApp.Cliente], await usuarios.GetRolesAsync(pendiente));
        Assert.True(await usuarios.CheckPasswordAsync(pendiente, opciones.Password));

        var logs = string.Join(Environment.NewLine, logger.Mensajes);
        Assert.DoesNotContain(opciones.Password, logs, StringComparison.Ordinal);
        foreach (var email in opciones.Correos.Values)
        {
            Assert.DoesNotContain(email, logs, StringComparison.Ordinal);
        }
    }

    private static OpcionesSeedUsuariosDesarrollo OpcionesValidas()
    {
        var sufijo = Guid.NewGuid().ToString("N");
        string Email(string alias) => $"{alias}-{sufijo}@example.invalid";
        return new OpcionesSeedUsuariosDesarrollo
        {
            Habilitado = true,
            Password = "Clave-Sintetica-123!",
            Correos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SeederUsuariosDesarrollo.AliasAdminPlataforma] = Email(SeederUsuariosDesarrollo.AliasAdminPlataforma),
                [SeederUsuariosDesarrollo.AliasRevisorKyc] = Email(SeederUsuariosDesarrollo.AliasRevisorKyc),
                [SeederUsuariosDesarrollo.AliasModerador] = Email(SeederUsuariosDesarrollo.AliasModerador),
                [SeederUsuariosDesarrollo.AliasSoporte] = Email(SeederUsuariosDesarrollo.AliasSoporte),
                [SeederUsuariosDesarrollo.AliasVendedor1] = Email(SeederUsuariosDesarrollo.AliasVendedor1),
                [SeederUsuariosDesarrollo.AliasVendedor2] = Email(SeederUsuariosDesarrollo.AliasVendedor2),
                [SeederUsuariosDesarrollo.AliasComprador1] = Email(SeederUsuariosDesarrollo.AliasComprador1),
                [SeederUsuariosDesarrollo.AliasComprador2] = Email(SeederUsuariosDesarrollo.AliasComprador2),
                [SeederUsuariosDesarrollo.AliasEmailPendiente] = Email(SeederUsuariosDesarrollo.AliasEmailPendiente),
                [SeederUsuariosDesarrollo.AliasKycNoIniciado] = Email(SeederUsuariosDesarrollo.AliasKycNoIniciado),
                [SeederUsuariosDesarrollo.AliasKycPendiente] = Email(SeederUsuariosDesarrollo.AliasKycPendiente),
                [SeederUsuariosDesarrollo.AliasKycRechazado] = Email(SeederUsuariosDesarrollo.AliasKycRechazado),
            },
        };
    }

    private static async Task AssertCuentaAsync(
        UserManager<ApplicationUser> usuarios,
        IConsultaVerificacionKyc consultaKyc,
        OpcionesSeedUsuariosDesarrollo opciones,
        string alias,
        string rol,
        bool emailConfirmado,
        bool kycAprobado)
    {
        var usuario = await usuarios.FindByEmailAsync(opciones.Correos[alias]);
        Assert.NotNull(usuario);
        Assert.Equal(emailConfirmado, usuario.EmailConfirmed);
        Assert.Equal([rol], await usuarios.GetRolesAsync(usuario));
        Assert.Equal(kycAprobado, await consultaKyc.EstaVerificadoAsync(usuario.Id, CancellationToken.None));
    }

    private static Task<EstadoKyc?> EstadoActualAsync(IdentityDbContext db, Guid usuarioId) =>
        db.VerificacionesKyc
            .Where(v => v.Id == usuarioId)
            .SelectMany(v => v.Solicitudes)
            .OrderByDescending(s => s.EnviadaEn)
            .Select(s => (EstadoKyc?)s.Estado)
            .FirstOrDefaultAsync();

    private sealed class LoggerCaptura<T> : ILogger<T>
    {
        public List<string> Mensajes { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensajes.Add(formatter(state, exception));
    }
}
