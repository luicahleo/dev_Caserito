using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>
/// Grupo minimal API <c>/api/admin</c>, protegido por la policy del permiso <c>usuarios.gestionar</c>:
/// ping de ejemplo, catálogo de roles, búsqueda de usuarios y gestión de roles por usuario.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Mapea el grupo <c>/api/admin</c> protegido por la policy del permiso <c>usuarios.gestionar</c>.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.UsuariosGestionar));

        grupo.MapGet("/ping", () => Results.Ok(new { estado = "ok" }));
        grupo.MapGet("/roles", ListarRolesAsync);

        return app;
    }

    private static async Task<IResult> ListarRolesAsync(ISender sender, CancellationToken ct)
    {
        var roles = await sender.Send(new ListarRolesQuery(), ct);
        return Results.Ok(roles);
    }
}
