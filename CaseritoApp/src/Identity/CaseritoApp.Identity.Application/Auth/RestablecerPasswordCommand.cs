using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Restablece una contraseña mediante un token temporal.</summary>
public sealed record RestablecerPasswordCommand(
    Guid UsuarioId,
    string Token,
    string Password) : ICommand;

/// <summary>Aplica el cambio y revoca sesiones únicamente después de un resultado exitoso.</summary>
public sealed class RestablecerPasswordCommandHandler(
    IRepositorioRestablecimientoPassword repositorio,
    IRevocadorSesionesUsuario revocadorSesiones)
    : ICommandHandler<RestablecerPasswordCommand>
{
    public async Task<Result> Handle(
        RestablecerPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var resultado = await repositorio.RestablecerAsync(
            request.UsuarioId,
            request.Token,
            request.Password,
            cancellationToken);
        if (!resultado.EsExito)
        {
            return Result.Fallo(resultado.Error);
        }

        await revocadorSesiones.RevocarTodasAsync(resultado.Valor, cancellationToken);
        return Result.Exito();
    }
}
