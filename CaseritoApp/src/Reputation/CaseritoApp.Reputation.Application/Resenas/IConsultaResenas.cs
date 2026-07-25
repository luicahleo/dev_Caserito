namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record EstadoPersistidoResenas(
    bool AutorYaCalifico,
    bool ContraparteYaCalifico,
    DateTimeOffset? EnviadaEn);

public interface IConsultaResenas
{
    public Task<EstadoPersistidoResenas> ObtenerEstadoAsync(
        Guid orderId,
        Guid autorId,
        Guid contraparteId,
        CancellationToken ct);

    public Task<ResumenReputacionDto> ObtenerResumenAsync(
        Guid usuarioId,
        CancellationToken ct);

    public Task<ResultadoPaginadoResenasDto> ListarPublicasAsync(
        Guid usuarioId,
        int pagina,
        int tamano,
        CancellationToken ct);
}
