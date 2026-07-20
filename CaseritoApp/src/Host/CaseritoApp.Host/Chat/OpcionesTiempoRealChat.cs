namespace CaseritoApp.Host.Chat;

public sealed class OpcionesTiempoRealChat
{
    public const string Seccion = "Chat:TiempoReal";

    public int MaximoConversaciones { get; init; } = 20;

    public int MaximoInvocacionesPorMinuto { get; init; } = 60;

    public int TamanoMaximoMensajeBytes { get; init; } = 16 * 1024;

    public int CapacidadBuffer { get; init; } = 10;

    public long BufferAplicacionBytes { get; init; } = 32 * 1024;

    public long BufferTransporteBytes { get; init; } = 32 * 1024;

    public int SegundosHandshake { get; init; } = 10;

    public int TamanoLote { get; init; } = 20;

    public int SegundosLease { get; init; } = 30;

    public int MilisegundosSondeo { get; init; } = 500;
}
