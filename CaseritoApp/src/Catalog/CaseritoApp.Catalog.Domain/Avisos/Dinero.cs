using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Value object de precio: un monto positivo en una moneda. Igualdad por valor.</summary>
public sealed record Dinero
{
    // Constructor sin parámetros para EF Core (owned type).
    private Dinero()
    {
    }

    /// <summary>Monto del precio; siempre mayor a cero.</summary>
    public decimal Monto { get; private init; }

    /// <summary>Moneda del precio.</summary>
    public Moneda Moneda { get; private init; }

    /// <summary>Crea un <see cref="Dinero"/> validando que el monto sea mayor a cero.</summary>
    public static Result<Dinero> Crear(decimal monto, Moneda moneda) =>
        monto <= 0
            ? Result.Fallo<Dinero>(new Error(ErroresAviso.PrecioInvalido, "El precio debe ser mayor a cero."))
            : Result.Exito(new Dinero { Monto = monto, Moneda = moneda });
}
