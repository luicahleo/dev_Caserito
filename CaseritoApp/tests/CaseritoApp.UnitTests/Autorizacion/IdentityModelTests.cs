using CaseritoApp.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class IdentityModelTests
{
    [Fact]
    public void Perfil_configura_campos_nuevos_obligatorios()
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-model-{Guid.NewGuid():N}")
            .Options;
        using var db = new IdentityDbContext(opciones);
        var usuario = db.Model.FindEntityType(typeof(ApplicationUser));

        Assert.NotNull(usuario);
        Assert.Equal(100, usuario.FindProperty(nameof(ApplicationUser.Nombres))?.GetMaxLength());
        Assert.False(usuario.FindProperty(nameof(ApplicationUser.Nombres))?.IsNullable);
        Assert.Equal(100, usuario.FindProperty(nameof(ApplicationUser.Apellidos))?.GetMaxLength());
        Assert.False(usuario.FindProperty(nameof(ApplicationUser.Apellidos))?.IsNullable);
        Assert.False(usuario.FindProperty(nameof(ApplicationUser.CiudadId))?.IsNullable);
    }
}
