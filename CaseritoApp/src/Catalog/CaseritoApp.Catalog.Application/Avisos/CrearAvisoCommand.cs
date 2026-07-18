using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Crea un aviso a nombre del vendedor autenticado. Requiere estar verificado (KYC).</summary>
public sealed record CrearAvisoCommand(
    Guid VendedorId,
    bool EstaVerificado,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Condicion,
    Guid CategoriaId,
    Guid CiudadId) : ICommand<Guid>;

/// <summary>Handler de <see cref="CrearAvisoCommand"/>.</summary>
public sealed class CrearAvisoCommandHandler(
    IRepositorioAvisos repositorio, IConsultaCatalogo catalogo, TimeProvider reloj)
    : ICommandHandler<CrearAvisoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearAvisoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EstaVerificado)
        {
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.NoVerificado, "Debe completar la verificación de identidad para publicar."));
        }

        var precio = Dinero.Crear(request.Monto, Moneda.BOB);
        if (!precio.EsExito)
        {
            return Result.Fallo<Guid>(precio.Error);
        }

        if (!await catalogo.ExisteCategoriaActivaAsync(request.CategoriaId, cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CategoriaInvalida, "La categoría no es válida."));
        }

        if (!await catalogo.ExisteCiudadActivaAsync(request.CiudadId, cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CiudadInvalida, "La ciudad no es válida."));
        }

        if (!Enum.TryParse<CondicionArticulo>(request.Condicion, out var condicion))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CondicionInvalida, "La condición no es válida."));
        }

        var aviso = Aviso.Crear(
            request.VendedorId, request.Titulo, request.Descripcion, precio.Valor,
            request.CategoriaId, request.CiudadId, condicion, reloj.GetUtcNow().UtcDateTime);

        repositorio.Agregar(aviso);
        return Result.Exito(aviso.Id);
    }
}

/// <summary>Valida <see cref="CrearAvisoCommand"/>.</summary>
public sealed class CrearAvisoCommandValidator : AbstractValidator<CrearAvisoCommand>
{
    public CrearAvisoCommandValidator()
    {
        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(120).WithMessage("El título no puede superar los 120 caracteres.");

        RuleFor(c => c.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000).WithMessage("La descripción no puede superar los 2000 caracteres.");

        RuleFor(c => c.Monto)
            .GreaterThan(0).WithMessage("El precio debe ser mayor a cero.");

        RuleFor(c => c.Condicion)
            .Must(v => Enum.TryParse<CondicionArticulo>(v, out _))
            .WithMessage("La condición no es válida.");

        RuleFor(c => c.CategoriaId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(c => c.CiudadId)
            .NotEmpty().WithMessage("La ciudad es obligatoria.");
    }
}
