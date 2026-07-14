using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Perfil;

/// <summary>Actualiza nombre y ciudad del perfil del usuario autenticado.</summary>
public sealed record ActualizarPerfilCommand(Guid UserId, string Nombre, string Ciudad) : ICommand;

/// <summary>Handler de <see cref="ActualizarPerfilCommand"/>: delega en <see cref="IRepositorioPerfil"/>.</summary>
public sealed class ActualizarPerfilCommandHandler(IRepositorioPerfil repositorioPerfil)
    : ICommandHandler<ActualizarPerfilCommand>
{
    public Task<Result> Handle(ActualizarPerfilCommand request, CancellationToken cancellationToken) =>
        repositorioPerfil.ActualizarAsync(request.UserId, request.Nombre, request.Ciudad, cancellationToken);
}

/// <summary>Valida <see cref="ActualizarPerfilCommand"/>: nombre y ciudad no vacíos y de longitud razonable.</summary>
public sealed class ActualizarPerfilCommandValidator : AbstractValidator<ActualizarPerfilCommand>
{
    public ActualizarPerfilCommandValidator()
    {
        RuleFor(c => c.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");

        RuleFor(c => c.Ciudad)
            .NotEmpty().WithMessage("La ciudad es obligatoria.")
            .MaximumLength(100).WithMessage("La ciudad no puede superar los 100 caracteres.");
    }
}
