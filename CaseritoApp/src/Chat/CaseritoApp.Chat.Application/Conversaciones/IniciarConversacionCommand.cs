using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record IniciarConversacionCommand(
    Guid CompradorId,
    Guid AvisoId) : ICommand<IniciarConversacionResultadoDto>;

public sealed class IniciarConversacionCommandHandler(
    IRepositorioConversaciones repositorio,
    IConsultaAvisoContactable consultaAviso,
    TimeProvider reloj,
    IRepositorioBloqueosUsuario bloqueos)
    : ICommandHandler<IniciarConversacionCommand, IniciarConversacionResultadoDto>
{
    public async Task<Result<IniciarConversacionResultadoDto>> Handle(
        IniciarConversacionCommand request,
        CancellationToken cancellationToken)
    {
        var existente = await repositorio.ObtenerPorCompradorAvisoAsync(
            request.CompradorId,
            request.AvisoId,
            cancellationToken);
        if (existente is not null)
        {
            if (await bloqueos.ExisteEntreAsync(
                existente.CompradorId,
                existente.VendedorId,
                cancellationToken))
            {
                return Result.Fallo<IniciarConversacionResultadoDto>(new Error(
                    ErroresConversacion.NoDisponibleParaEnvio,
                    "La conversaciÃ³n no estÃ¡ disponible para enviar mensajes."));
            }

            return Result.Exito(new IniciarConversacionResultadoDto(
                ConversacionDto.Desde(existente),
                false));
        }

        var aviso = await consultaAviso.ObtenerAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.AvisoId != request.AvisoId)
        {
            return Result.Fallo<IniciarConversacionResultadoDto>(new Error(
                ErroresConversacion.AvisoNoContactable,
                "El aviso no está disponible."));
        }

        if (await bloqueos.ExisteEntreAsync(
            request.CompradorId,
            aviso.VendedorId,
            cancellationToken))
        {
            return Result.Fallo<IniciarConversacionResultadoDto>(new Error(
                ErroresConversacion.NoDisponibleParaEnvio,
                "La conversación no está disponible para enviar mensajes."));
        }

        var resultadoCreacion = Conversacion.Crear(
            aviso.AvisoId,
            request.CompradorId,
            aviso.VendedorId,
            reloj.GetUtcNow());
        if (!resultadoCreacion.EsExito)
        {
            return Result.Fallo<IniciarConversacionResultadoDto>(resultadoCreacion.Error);
        }

        repositorio.Agregar(resultadoCreacion.Valor);
        return Result.Exito(new IniciarConversacionResultadoDto(
            ConversacionDto.Desde(resultadoCreacion.Valor),
            true));
    }
}

public sealed class IniciarConversacionCommandValidator : AbstractValidator<IniciarConversacionCommand>
{
    public IniciarConversacionCommandValidator()
    {
        RuleFor(c => c.CompradorId)
            .NotEmpty().WithMessage("El comprador es obligatorio.");
        RuleFor(c => c.AvisoId)
            .NotEmpty().WithMessage("El aviso es obligatorio.");
    }
}
