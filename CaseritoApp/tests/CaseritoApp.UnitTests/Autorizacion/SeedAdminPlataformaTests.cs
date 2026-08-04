using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class SeedAdminPlataformaTests
{
    private static readonly Guid _ciudadId = new("22222222-2222-2222-2222-000000000001");

    private static UserManager<ApplicationUser> CrearUserManagerSustituto()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static OpcionesSeedAdmin OpcionesCompletas() => new()
    {
        AdminEmail = "admin@caserito.test",
        AdminPassword = "Clave$ecreta1",
        AdminNombres = "Ana María",
        AdminApellidos = "Administradora",
        AdminCiudadId = _ciudadId,
    };

    private static SeedAdminPlataforma CrearSeed(UserManager<ApplicationUser> usuarios)
        => new(usuarios, NullLogger<SeedAdminPlataforma>.Instance);

    [Fact]
    public async Task Sin_configuracion_completa_no_crea_nada()
    {
        var usuarios = CrearUserManagerSustituto();

        await CrearSeed(usuarios).EjecutarAsync(new OpcionesSeedAdmin(), CancellationToken.None);

        await usuarios.DidNotReceive().CreateAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Admin_ya_existente_es_no_op()
    {
        var usuarios = CrearUserManagerSustituto();
        var existente = new ApplicationUser { Email = "admin@caserito.test" };
        usuarios.FindByEmailAsync("admin@caserito.test").Returns(existente);
        usuarios.IsInRoleAsync(existente, RolesApp.AdminPlataforma).Returns(true);

        await CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None);

        await usuarios.DidNotReceive().CreateAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
        await usuarios.DidNotReceive().AddToRoleAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Admin_existente_sin_rol_repara_asignacion()
    {
        var usuarios = CrearUserManagerSustituto();
        var existente = new ApplicationUser { Email = "admin@caserito.test" };
        usuarios.FindByEmailAsync("admin@caserito.test").Returns(existente);
        usuarios.IsInRoleAsync(existente, RolesApp.AdminPlataforma).Returns(false);
        usuarios.AddToRoleAsync(existente, RolesApp.AdminPlataforma)
            .Returns(IdentityResult.Success);

        await CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None);

        await usuarios.DidNotReceive().CreateAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
        await usuarios.Received(1).AddToRoleAsync(existente, RolesApp.AdminPlataforma);
    }

    [Fact]
    public async Task Crea_admin_confirmado_con_rol_admin_plataforma()
    {
        var usuarios = CrearUserManagerSustituto();
        usuarios.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        usuarios.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        usuarios.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        await CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None);

        await usuarios.Received(1).CreateAsync(
            Arg.Is<ApplicationUser>(usuario =>
                usuario.Email == "admin@caserito.test"
                && usuario.Nombres == "Ana María"
                && usuario.Apellidos == "Administradora"
                && usuario.CiudadId == _ciudadId
                && usuario.EmailConfirmed),
            "Clave$ecreta1");
        await usuarios.Received(1).AddToRoleAsync(
            Arg.Any<ApplicationUser>(), RolesApp.AdminPlataforma);
    }

    [Fact]
    public async Task Fallo_de_contrasena_no_lanza_excepcion()
    {
        var usuarios = CrearUserManagerSustituto();
        usuarios.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        usuarios.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(
                new IdentityError { Code = "PasswordTooShort", Description = "x" }));

        var excepcion = await Record.ExceptionAsync(
            () => CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None));

        Assert.Null(excepcion);
        await usuarios.DidNotReceive().AddToRoleAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }
}
