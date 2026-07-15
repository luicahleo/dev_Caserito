using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;

namespace CaseritoApp.Host.Endpoints;

/// <summary>
/// Grupo minimal API <c>/api/admin</c>. Por ahora solo expone <c>GET /ping</c> como andamiaje
/// demostrativo del pipeline RBAC (policy de permiso). Los endpoints reales de administración
/// (moderación, revisión KYC) llegan en Fase 2.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Mapea el grupo <c>/api/admin</c> protegido por la policy del permiso <c>usuarios.gestionar</c>.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.UsuariosGestionar));

        grupo.MapGet("/ping", () => Results.Ok(new { estado = "ok" }));

        return app;
    }
}
