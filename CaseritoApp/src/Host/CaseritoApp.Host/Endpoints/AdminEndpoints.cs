using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo para asignar un rol a un usuario.</summary>
public sealed record AsignarRolRequest(string Rol);

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
        grupo.MapGet("/usuarios", BuscarUsuariosAsync);
        grupo.MapPost("/usuarios/{id:guid}/roles", AsignarRolAsync);
        grupo.MapDelete("/usuarios/{id:guid}/roles/{rol}", QuitarRolAsync);

        return app;
    }

    private static async Task<IResult> ListarRolesAsync(ISender sender, CancellationToken ct)
    {
        var roles = await sender.Send(new ListarRolesQuery(), ct);
        return Results.Ok(roles);
    }

    private static async Task<IResult> BuscarUsuariosAsync(
        ISender sender, CancellationToken ct, string? query = null, int pagina = 1, int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(new BuscarUsuariosQuery(query, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }

    private static async Task<IResult> AsignarRolAsync(
        Guid id, AsignarRolRequest request, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerAdminId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new AsignarRolCommand(adminId, id, request.Rol), ct);
            return DesdeResult(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }

    private static async Task<IResult> QuitarRolAsync(
        Guid id, string rol, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerAdminId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new QuitarRolCommand(adminId, id, rol), ct);
            return DesdeResult(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }

    private static bool TryObtenerAdminId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    // Mapea el Result de los comandos de gestión de roles a códigos HTTP.
    private static IResult DesdeResult(Result resultado)
    {
        if (resultado.EsExito)
        {
            return Results.NoContent();
        }

        return resultado.Error.Code switch
        {
            CodigosErrorRoles.UsuarioNoEncontrado =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status404NotFound),
            CodigosErrorRoles.UltimoAdminPlataforma =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status409Conflict),
            _ =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status400BadRequest),
        };
    }
}
