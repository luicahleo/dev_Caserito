using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Edita un aviso propio.</summary>
public sealed record EditarAvisoCommand(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Condicion,
    Guid CategoriaId,
    Guid CiudadId) : ICommand;

/// <summary>Handler de <see cref="EditarAvisoCommand"/>.</summary>
public sealed class EditarAvisoCommandHandler(
    IRepositorioAvisos repositorio, IConsultaCatalogo catalogo, TimeProvider reloj)
    : ICommandHandler<EditarAvisoCommand>
{
    public async Task<Result> Handle(EditarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        var precio = Dinero.Crear(request.Monto, Moneda.BOB);
        if (!precio.EsExito)
        {
            return precio;
        }

        if (!await catalogo.ExisteCategoriaActivaAsync(request.CategoriaId, cancellationToken))
        {
            return Result.Fallo(new Error(ErroresAviso.CategoriaInvalida, "La categoría no es válida."));
        }

        if (!await catalogo.ExisteCiudadActivaAsync(request.CiudadId, cancellationToken))
        {
            return Result.Fallo(new Error(ErroresAviso.CiudadInvalida, "La ciudad no es válida."));
        }

        var condicion = Enum.Parse<CondicionArticulo>(request.Condicion);
        return aviso.Editar(
            request.Titulo, request.Descripcion, precio.Valor,
            request.CategoriaId, request.CiudadId, condicion, reloj.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Valida <see cref="EditarAvisoCommand"/>.</summary>
public sealed class EditarAvisoCommandValidator : AbstractValidator<EditarAvisoCommand>
{
    public EditarAvisoCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(120).WithMessage("El título no puede superar los 120 caracteres.");
        RuleFor(c => c.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000).WithMessage("La descripción no puede superar los 2000 caracteres.");
        RuleFor(c => c.Monto).GreaterThan(0).WithMessage("El precio debe ser mayor a cero.");
        RuleFor(c => c.Condicion)
            .Must(v => Enum.TryParse<CondicionArticulo>(v, out _))
            .WithMessage("La condición no es válida.");
        RuleFor(c => c.CategoriaId).NotEmpty().WithMessage("La categoría es obligatoria.");
        RuleFor(c => c.CiudadId).NotEmpty().WithMessage("La ciudad es obligatoria.");
    }
}
