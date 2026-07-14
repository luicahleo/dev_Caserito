using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Perfil;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo de la solicitud de actualización de perfil.</summary>
public sealed record ActualizarPerfilRequest(string Nombre, string Ciudad);

/// <summary>Registro y mapeo del grupo minimal API <c>/api/perfil</c>: ver y editar el perfil del usuario autenticado.</summary>
public static class PerfilEndpoints
{
    /// <summary>Mapea los endpoints de perfil bajo el prefijo <c>/api/perfil</c>, ambos <c>[Authorize]</c>.</summary>
    public static IEndpointRouteBuilder MapPerfilEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/perfil").RequireAuthorization();

        grupo.MapGet("/", ObtenerAsync);
        grupo.MapPut("/", ActualizarAsync);

        return app;
    }

    private static async Task<IResult> ObtenerAsync(
        ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerPerfilQuery(userId), ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : ProblemaDesdeError(resultado.Error);
    }

    private static async Task<IResult> ActualizarAsync(
        ActualizarPerfilRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new ActualizarPerfilCommand(userId, request.Nombre, request.Ciudad), ct);

            return resultado.EsExito
                ? Results.Ok()
                : ProblemaDesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            // El ValidationBehavior de MediatR lanza ValidationException cuando el comando no
            // pasa las reglas de FluentValidation; aquí se traduce a un 400 ProblemDetails.
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(errores);
        }
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out userId);
    }

    private static IResult ProblemaDesdeError(Error error) =>
        Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
