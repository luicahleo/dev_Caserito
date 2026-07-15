using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class QuitarRolCommandValidatorTests
{
    private readonly QuitarRolCommandValidator _validator = new();

    [Fact]
    public void Acepta_quitar_rol_conocido_a_otro_usuario()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Moderador);

        Assert.True(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_desconocido()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), "RolFalso");

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_Sistema()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Sistema);

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_auto_retiro_de_AdminPlataforma()
    {
        var mismoId = Guid.NewGuid();
        var cmd = new QuitarRolCommand(mismoId, mismoId, RolesApp.AdminPlataforma);

        Assert.False(_validator.Validate(cmd).IsValid);
    }
}
