using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class MapaRolesPermisosTests
{
    [Fact]
    public void Cliente_no_tiene_permisos_elevados()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.Cliente]);

        Assert.Empty(permisos);
    }

    [Fact]
    public void AdminPlataforma_tiene_todos_los_permisos()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.AdminPlataforma]);

        Assert.Equal(Permisos.Todos.Count, permisos.Count);
        Assert.All(Permisos.Todos, p => Assert.Contains(p, permisos));
    }

    [Fact]
    public void Usuario_multi_rol_une_permisos_sin_duplicados()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.Moderador, RolesApp.AdminKyc]);

        Assert.Contains(Permisos.PublicacionesModerar, permisos);
        Assert.Contains(Permisos.ChatModerar, permisos);
        Assert.Contains(Permisos.KycRevisar, permisos);
        Assert.Equal(3, permisos.Count);
    }

    [Fact]
    public void Rol_desconocido_se_ignora()
    {
        var permisos = MapaRolesPermisos.PermisosDe(["RolInexistente"]);

        Assert.Empty(permisos);
    }
}
