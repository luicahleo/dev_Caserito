using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Authorization;

namespace CaseritoApp.Host.Auth;

/// <summary>Requisito de autorización: el usuario autenticado tiene el correo confirmado.</summary>
public sealed class RequisitoEmailConfirmado : IAuthorizationRequirement;

/// <summary>
/// Autoriza cuando el principal lleva el claim <c>emailConfirmed</c> en <c>true</c>.
/// El claim se emite en el JWT a partir de <c>ApplicationUser.EmailConfirmed</c>, así que
/// un usuario que confirma su correo necesita un token nuevo (login o refresh) para desbloquear
/// las acciones protegidas.
/// </summary>
public sealed class EmailConfirmadoHandler : AuthorizationHandler<RequisitoEmailConfirmado>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequisitoEmailConfirmado requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(ClaimsApp.EmailConfirmado, "true"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
