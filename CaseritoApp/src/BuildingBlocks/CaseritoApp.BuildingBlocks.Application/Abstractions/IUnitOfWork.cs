namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Abstracción para persistir los cambios acumulados durante el manejo de un comando.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Guarda los cambios pendientes en el almacén de datos.
    /// </summary>
    /// <returns>Número de entidades afectadas.</returns>
    public Task<int> GuardarCambiosAsync(CancellationToken ct);
}
