using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Orders.Domain.Ordenes;

public sealed class Orden : AggregateRoot
{
    private Orden()
    {
        Moneda = null!;
        Version = null!;
    }

    private Orden(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        decimal montoAcordado,
        string moneda,
        DateTimeOffset creadaEn)
    {
        AvisoId = avisoId;
        CompradorId = compradorId;
        VendedorId = vendedorId;
        MontoAcordado = montoAcordado;
        Moneda = moneda;
        Estado = EstadoOrden.Requested;
        CreadaEn = creadaEn;
        ActualizadaEn = creadaEn;
        Version = [];
    }

    public Guid AvisoId { get; private set; }

    public Guid CompradorId { get; private set; }

    public Guid VendedorId { get; private set; }

    public EstadoOrden Estado { get; private set; }

    public decimal MontoAcordado { get; private set; }

    public string Moneda { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public byte[] Version { get; private set; }

    public static Result<Orden> Crear(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        decimal montoAcordado,
        string moneda,
        DateTimeOffset ocurrioEn)
    {
        if (avisoId == Guid.Empty || compradorId == Guid.Empty || vendedorId == Guid.Empty
            || montoAcordado <= 0)
        {
            return AcuerdoInvalido();
        }

        if (compradorId == vendedorId)
        {
            return Result.Fallo<Orden>(new Error(
                ErroresOrden.ParticipantesCoinciden,
                "Los participantes del acuerdo deben ser distintos."));
        }

        var monedaNormalizada = moneda?.Trim().ToUpperInvariant();
        if (monedaNormalizada is null
            || monedaNormalizada.Length != 3
            || monedaNormalizada.Any(caracter => caracter is < 'A' or > 'Z'))
        {
            return AcuerdoInvalido();
        }

        var fechaUtc = ocurrioEn.ToUniversalTime();
        var orden = new Orden(
            avisoId,
            compradorId,
            vendedorId,
            montoAcordado,
            monedaNormalizada,
            fechaUtc);
        orden.AgregarEvento(new OrdenSolicitada(orden.Id, avisoId, fechaUtc));
        return Result.Exito(orden);
    }

    public Result Aceptar(Guid actorId, DateTimeOffset ocurrioEn)
    {
        if (actorId == Guid.Empty || actorId != VendedorId)
        {
            return NoEncontrada();
        }

        if (Estado == EstadoOrden.Agreed)
        {
            return Result.Exito();
        }

        if (Estado != EstadoOrden.Requested)
        {
            return Result.Fallo(new Error(
                ErroresOrden.TransicionInvalida,
                "La orden no admite esta transición."));
        }

        var fechaUtc = ocurrioEn.ToUniversalTime();
        var estadoAnterior = Estado;
        Estado = EstadoOrden.Agreed;
        ActualizadaEn = fechaUtc;
        AgregarEvento(new EstadoOrdenCambiado(
            Id,
            estadoAnterior,
            Estado,
            fechaUtc));
        return Result.Exito();
    }

    public Result Cancelar(Guid actorId, DateTimeOffset ocurrioEn)
    {
        if (!EsParticipante(actorId))
        {
            return NoEncontrada();
        }

        if (Estado == EstadoOrden.Cancelled)
        {
            return Result.Exito();
        }

        if (Estado is not (EstadoOrden.Requested or EstadoOrden.Agreed))
        {
            return Result.Fallo(new Error(
                ErroresOrden.TransicionInvalida,
                "La orden no admite esta transición."));
        }

        var fechaUtc = ocurrioEn.ToUniversalTime();
        var estadoAnterior = Estado;
        Estado = EstadoOrden.Cancelled;
        ActualizadaEn = fechaUtc;
        AgregarEvento(new EstadoOrdenCambiado(
            Id,
            estadoAnterior,
            Estado,
            fechaUtc));
        return Result.Exito();
    }

    public bool EsParticipante(Guid usuarioId) =>
        usuarioId != Guid.Empty && (usuarioId == CompradorId || usuarioId == VendedorId);

    private static Result<Orden> AcuerdoInvalido() =>
        Result.Fallo<Orden>(new Error(
            ErroresOrden.AcuerdoInvalido,
            "Los datos del acuerdo no son válidos."));

    private static Result NoEncontrada() =>
        Result.Fallo(new Error(
            ErroresOrden.NoEncontrada,
            "La orden no está disponible."));
}
