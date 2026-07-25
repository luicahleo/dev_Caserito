namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record ResenaCreadaDto(Guid Id, DateTimeOffset CreadaEn);

public sealed record EstadoResenaOrdenDto(
    bool PuedeCalificar,
    bool AutorYaCalifico,
    bool ContraparteYaCalifico,
    bool Reveladas,
    DateTimeOffset? EnviadaEn,
    Guid ContraparteId);

public sealed record ResumenReputacionDto(decimal? Promedio, int Total);

public sealed record ResenaPublicaDto(
    int Puntuacion,
    string Comentario,
    DateTimeOffset CreadaEn,
    string RolAutor);

public sealed record ResultadoPaginadoResenasDto(
    IReadOnlyList<ResenaPublicaDto> Items,
    int Pagina,
    int Tamano,
    int Total);
