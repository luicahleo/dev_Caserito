namespace CaseritoApp.BuildingBlocks.Domain;

/// <summary>
/// Representa el resultado de una operación de dominio, indicando éxito o fallo.
/// </summary>
public class Result
{
    protected Result(bool esExito, Error error)
    {
        EsExito = esExito;
        Error = error;
    }

    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public bool EsExito { get; }

    /// <summary>
    /// Error asociado al resultado. Es <see cref="Domain.Error.None"/> cuando <see cref="EsExito"/> es <c>true</c>.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Crea un resultado exitoso sin valor asociado.
    /// </summary>
    public static Result Exito() => new(true, Error.None);

    /// <summary>
    /// Crea un resultado exitoso con un valor asociado.
    /// </summary>
    public static Result<T> Exito<T>(T valor) => Result<T>.Exito(valor);

    /// <summary>
    /// Crea un resultado fallido con el error indicado.
    /// </summary>
    public static Result Fallo(Error error) => new(false, error);

    /// <summary>
    /// Crea un resultado fallido con un valor tipado y el error indicado.
    /// </summary>
    public static Result<T> Fallo<T>(Error error) => Result<T>.Fallo(error);
}

/// <summary>
/// Representa el resultado de una operación de dominio que produce un valor de tipo <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Tipo del valor producido por la operación.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _valor;

    private Result(bool esExito, T? valor, Error error) : base(esExito, error) => _valor = valor;

    /// <summary>
    /// Valor producido por la operación. Lanza <see cref="InvalidOperationException"/> si el resultado no fue exitoso.
    /// </summary>
    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    /// <summary>
    /// Crea un resultado exitoso con el valor indicado.
    /// </summary>
    // CA1000 (no declarar miembros estáticos en tipos genéricos): suprimido de forma puntual porque
    // el brief exige explícitamente la API "Result<T>.Exito(T)" como fábrica estática del tipo.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Contrato requerido por el diseño: Result<T>.Exito(T) es la fábrica pública especificada.")]
    public static Result<T> Exito(T valor) => new(true, valor, Error.None);

    /// <summary>
    /// Crea un resultado fallido con el error indicado.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Contrato requerido por el diseño: Result<T>.Fallo(Error) es la fábrica pública especificada.")]
    public static new Result<T> Fallo(Error error) => new(false, default, error);

    /// <summary>
    /// Permite construir un <see cref="Result{T}"/> exitoso de forma implícita a partir de un valor.
    /// </summary>
    public static implicit operator Result<T>(T valor) => Exito(valor);
}
