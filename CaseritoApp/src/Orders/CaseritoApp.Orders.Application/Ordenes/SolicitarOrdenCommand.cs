using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record SolicitarOrdenCommand(
    Guid AvisoId,
    Guid CompradorId,
    bool CompradorVerificado) : ICommand<OrdenCreadaDto>;

public sealed class SolicitarOrdenCommandHandler(
    IRepositorioOrdenes repositorio,
    IConsultaAvisoParaOrden consultaAviso,
    IConsultaVerificacionParticipante consultaVerificacion)
    : ICommandHandler<SolicitarOrdenCommand, OrdenCreadaDto>
{
    public async Task<Result<OrdenCreadaDto>> Handle(
        SolicitarOrdenCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.CompradorVerificado
            || !await consultaVerificacion.EstaVerificadoAsync(
                request.CompradorId,
                cancellationToken))
        {
            return NoVerificado();
        }

        var aviso = await consultaAviso.ObtenerAsync(request.AvisoId, cancellationToken);
        if (aviso is null)
        {
            return Result.Fallo<OrdenCreadaDto>(new Error(
                ErroresOrden.NoEncontrada,
                "El aviso no está disponible."));
        }

        if (!await consultaVerificacion.EstaVerificadoAsync(
                aviso.VendedorId,
                cancellationToken))
        {
            return NoVerificado();
        }

        if (await repositorio.ExisteAbiertaAsync(
                request.AvisoId,
                request.CompradorId,
                cancellationToken))
        {
            return Result.Fallo<OrdenCreadaDto>(new Error(
                ErroresOrden.Duplicada,
                "Ya existe un acuerdo abierto para este aviso."));
        }

        var resultado = Orden.Crear(
            aviso.AvisoId,
            request.CompradorId,
            aviso.VendedorId,
            aviso.Monto,
            aviso.Moneda,
            DateTimeOffset.UtcNow);
        if (!resultado.EsExito)
        {
            return Result.Fallo<OrdenCreadaDto>(resultado.Error);
        }

        var orden = resultado.Valor;
        repositorio.Agregar(orden);
        return Result.Exito(new OrdenCreadaDto(
            orden.Id,
            orden.AvisoId,
            orden.Estado.ToString(),
            orden.MontoAcordado,
            orden.Moneda,
            orden.CreadaEn));
    }

    private static Result<OrdenCreadaDto> NoVerificado() =>
        Result.Fallo<OrdenCreadaDto>(new Error(
            ErroresOrden.NoVerificado,
            "Ambos participantes deben tener la identidad verificada."));
}

public sealed class SolicitarOrdenCommandValidator : AbstractValidator<SolicitarOrdenCommand>
{
    public SolicitarOrdenCommandValidator()
    {
        RuleFor(command => command.AvisoId).NotEmpty();
        RuleFor(command => command.CompradorId).NotEmpty();
    }
}
