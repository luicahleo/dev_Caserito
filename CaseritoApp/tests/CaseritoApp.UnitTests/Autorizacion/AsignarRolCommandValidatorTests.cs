using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class AsignarRolCommandValidatorTests
{
    private readonly AsignarRolCommandValidator _validator = new();

    [Fact]
    public void Acepta_rol_conocido()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Moderador);

        Assert.True(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_desconocido()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), "RolFalso");

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_Sistema()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Sistema);

        Assert.False(_validator.Validate(cmd).IsValid);
    }
}
