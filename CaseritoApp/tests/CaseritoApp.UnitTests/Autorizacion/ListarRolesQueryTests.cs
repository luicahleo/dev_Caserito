using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class ListarRolesQueryTests
{
    [Fact]
    public async Task Devuelve_los_seis_roles_con_sus_permisos()
    {
        var handler = new ListarRolesQueryHandler();

        var roles = await handler.Handle(new ListarRolesQuery(), CancellationToken.None);

        Assert.Equal(RolesApp.Todos.Count, roles.Count);

        var adminPlataforma = roles.Single(r => r.Rol == RolesApp.AdminPlataforma);
        Assert.Equal(Permisos.Todos.Count, adminPlataforma.Permisos.Count);

        var cliente = roles.Single(r => r.Rol == RolesApp.Cliente);
        Assert.Empty(cliente.Permisos);
    }
}
