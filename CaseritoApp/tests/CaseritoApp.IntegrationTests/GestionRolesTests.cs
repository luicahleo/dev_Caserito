using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Verifica los endpoints de gestión de roles bajo <c>/api/admin</c>.</summary>
public sealed class GestionRolesTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        if (rolExtra is not null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(usuario!, rolExtra);
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        return body!.AccessToken;
    }

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    private static string[] PermisosDelToken(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ToArray();
    }

    private async Task<(string email, Guid id)> RegistrarClienteAsync(HttpClient cliente)
    {
        var email = Email("target");
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        return (email, usuario!.Id);
    }

    [Fact]
    public async Task Listar_roles_devuelve_200_con_admin()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("roles-list"), RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/roles", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Listar_roles_sin_permiso_devuelve_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("roles-403"), rolExtra: null);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/roles", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Buscar_usuarios_devuelve_al_usuario_por_email()
    {
        using var cliente = factory.CreateClient();
        var email = Email("busqueda");
        var token = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(email)}", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.Contains(pagina!.Items, u => u.Email == email);
        Assert.Contains(pagina.Items, u => u.Roles.Contains(RolesApp.AdminPlataforma));
    }

    [Fact]
    public async Task Buscar_usuarios_con_paginacion_invalida_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("busqueda-400"), RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/usuarios?tamano=0", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_lo_refleja_en_la_busqueda_y_se_propaga_al_relogin()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-asignar"), RolesApp.AdminPlataforma);
        var (targetEmail, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        // Se refleja en la búsqueda.
        using var buscar = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(targetEmail)}", adminToken);
        var paginaResp = await cliente.SendAsync(buscar);
        var pagina = await paginaResp.Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.Contains(pagina!.Items, u => u.Id == targetId && u.Roles.Contains(RolesApp.Moderador));

        // Propagación: al (re)loguear, el JWT del target trae los permisos de Moderador.
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(targetEmail, "Password123!"));
        var tokenTarget = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        var permisos = PermisosDelToken(tokenTarget);
        Assert.Contains(Permisos.PublicacionesModerar, permisos);
        Assert.Contains(Permisos.ChatModerar, permisos);
    }

    [Fact]
    public async Task Asignar_rol_desconocido_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-rol-falso"), RolesApp.AdminPlataforma);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest("RolFalso"));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_Sistema_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-sistema"), RolesApp.AdminPlataforma);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Sistema));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_a_usuario_inexistente_devuelve_404()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-404"), RolesApp.AdminPlataforma);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{Guid.NewGuid()}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_sin_permiso_devuelve_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("no-admin"), rolExtra: null);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", token);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Quitar_rol_lo_elimina_de_la_busqueda()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-quitar"), RolesApp.AdminPlataforma);
        var (targetEmail, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(asignar)).StatusCode);

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{targetId}/roles/{RolesApp.Moderador}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(quitar)).StatusCode);

        using var buscar = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(targetEmail)}", adminToken);
        var pagina = await (await cliente.SendAsync(buscar)).Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.DoesNotContain(pagina!.Items, u => u.Id == targetId && u.Roles.Contains(RolesApp.Moderador));
    }

    // Nota: el 409 "último AdminPlataforma" (conteo global = 1) se cubre en unit (Task 4, Step 6b),
    // porque la BD de integración es compartida y suele tener varios AdminPlataforma. Aquí se verifica
    // el caso complementario: con otro admin presente, quitar AdminPlataforma a un tercero es 204.
    [Fact]
    public async Task Quitar_AdminPlataforma_con_otro_admin_presente_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        // El ejecutor es AdminPlataforma; garantiza que exista >1 admin en BD.
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-ejecutor"), RolesApp.AdminPlataforma);

        // El objetivo también es AdminPlataforma (distinto del ejecutor): quitarle el rol no lo deja sin admins.
        var (_, targetId) = await RegistrarClienteAsync(cliente);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var target = await userManager.FindByIdAsync(targetId.ToString());
            await userManager.AddToRoleAsync(target!, RolesApp.AdminPlataforma);
        }

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{targetId}/roles/{RolesApp.AdminPlataforma}", adminToken);
        var respuesta = await cliente.SendAsync(quitar);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Auto_retiro_de_AdminPlataforma_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var email = Email("admin-auto");
        var adminToken = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminPlataforma);

        Guid adminId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            adminId = (await userManager.FindByEmailAsync(email))!.Id;
        }

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{adminId}/roles/{RolesApp.AdminPlataforma}", adminToken);
        var respuesta = await cliente.SendAsync(quitar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}

sealed file record UsuarioConRolesResponse(Guid Id, string Email, string Nombre, string Ciudad, string[] Roles);

sealed file record PaginaUsuariosResponse(UsuarioConRolesResponse[] Items, int Pagina, int Tamano, int Total);
