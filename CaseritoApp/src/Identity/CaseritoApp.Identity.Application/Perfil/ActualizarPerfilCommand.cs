using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>Actualiza nombres, apellidos y ciudad del perfil autenticado.</summary>
public sealed record ActualizarPerfilCommand(
    Guid UserId,
    string Nombres,
    string Apellidos,
    Guid CiudadId) : ICommand;

/// <summary>Handler de <see cref="ActualizarPerfilCommand"/>: delega en <see cref="IRepositorioPerfil"/>.</summary>
public sealed class ActualizarPerfilCommandHandler(IRepositorioPerfil repositorioPerfil)
    : ICommandHandler<ActualizarPerfilCommand>
{
    public Task<Result> Handle(ActualizarPerfilCommand request, CancellationToken cancellationToken) =>
        repositorioPerfil.ActualizarAsync(
            request.UserId,
            request.Nombres,
            request.Apellidos,
            request.CiudadId,
            cancellationToken);
}

/// <summary>Valida <see cref="ActualizarPerfilCommand"/>: nombre y ciudad no vacíos y de longitud razonable.</summary>
public sealed class ActualizarPerfilCommandValidator : AbstractValidator<ActualizarPerfilCommand>
{
    public ActualizarPerfilCommandValidator()
    {
        RuleFor(c => c.Nombres)
            .NotEmpty().WithMessage("Los nombres son obligatorios.")
            .MaximumLength(100).WithMessage("Los nombres no pueden superar los 100 caracteres.");

        RuleFor(c => c.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son obligatorios.")
            .MaximumLength(100).WithMessage("Los apellidos no pueden superar los 100 caracteres.");

        RuleFor(c => c.CiudadId)
            .NotEmpty().WithMessage("La ciudad es obligatoria.");
    }
}
