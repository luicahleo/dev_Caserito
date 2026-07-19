using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Paginacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Mensajes;

public sealed record ObtenerMensajesQuery(
    Guid ConversacionId,
    Guid UsuarioId,
    long? AntesDeSecuencia,
    int Limite = 50) : IQuery<Result<PaginaCursor<MensajeDto, long>>>;

public sealed class ObtenerMensajesQueryHandler(IConsultaMensajes consulta)
    : IQueryHandler<ObtenerMensajesQuery, Result<PaginaCursor<MensajeDto, long>>>
{
    public async Task<Result<PaginaCursor<MensajeDto, long>>> Handle(
        ObtenerMensajesQuery request,
        CancellationToken cancellationToken)
    {
        var pagina = await consulta.ListarAsync(
            request.ConversacionId,
            request.UsuarioId,
            request.AntesDeSecuencia,
            request.Limite,
            cancellationToken);

        return pagina is null
            ? Result.Fallo<PaginaCursor<MensajeDto, long>>(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."))
            : Result.Exito(pagina);
    }
}

public sealed class ObtenerMensajesQueryValidator : AbstractValidator<ObtenerMensajesQuery>
{
    public ObtenerMensajesQueryValidator()
    {
        RuleFor(q => q.ConversacionId)
            .NotEmpty().WithMessage("La conversación es obligatoria.");
        RuleFor(q => q.UsuarioId)
            .NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(q => q.AntesDeSecuencia)
            .GreaterThan(0).When(q => q.AntesDeSecuencia.HasValue)
            .WithMessage("El cursor no es válido.");
        RuleFor(q => q.Limite)
            .InclusiveBetween(1, 100).WithMessage("El límite debe estar entre 1 y 100.");
    }
}
