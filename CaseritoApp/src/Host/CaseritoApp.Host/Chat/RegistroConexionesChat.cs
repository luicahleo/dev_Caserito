using System.Collections.Concurrent;

namespace CaseritoApp.Host.Chat;

public sealed class RegistroConexionesChat
{
    private readonly SemaphoreSlim[] _exclusiones =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly ConcurrentDictionary<string, ConexionRegistrada> _conexiones =
        new(StringComparer.Ordinal);

    public async Task EjecutarExclusivoAsync(
        Guid conversacionId,
        Func<Task> accion,
        CancellationToken cancellationToken)
    {
        var indice = (int)((uint)conversacionId.GetHashCode() % _exclusiones.Length);
        var exclusion = _exclusiones[indice];
        await exclusion.WaitAsync(cancellationToken);
        try
        {
            await accion();
        }
        finally
        {
            exclusion.Release();
        }
    }

    public void Registrar(Guid usuarioId, Guid conversacionId, string connectionId) =>
        _conexiones.AddOrUpdate(
            connectionId,
            _ => new ConexionRegistrada(usuarioId, [conversacionId]),
            (_, existente) =>
            {
                lock (existente.Conversaciones)
                {
                    existente.Conversaciones.Add(conversacionId);
                }

                return existente;
            });

    public void Quitar(Guid conversacionId, string connectionId)
    {
        if (!_conexiones.TryGetValue(connectionId, out var existente))
        {
            return;
        }

        lock (existente.Conversaciones)
        {
            existente.Conversaciones.Remove(conversacionId);
            if (existente.Conversaciones.Count == 0)
            {
                _conexiones.TryRemove(
                    new KeyValuePair<string, ConexionRegistrada>(connectionId, existente));
            }
        }
    }

    public void QuitarConexion(string connectionId) => _conexiones.TryRemove(connectionId, out _);

    public IReadOnlyList<string> ObtenerConexiones(
        IReadOnlySet<Guid> usuarios,
        Guid conversacionId)
    {
        var resultado = new List<string>();
        foreach (var (connectionId, registrada) in _conexiones)
        {
            if (!usuarios.Contains(registrada.UsuarioId))
            {
                continue;
            }

            lock (registrada.Conversaciones)
            {
                if (registrada.Conversaciones.Contains(conversacionId))
                {
                    resultado.Add(connectionId);
                }
            }
        }

        return resultado;
    }

    private sealed record ConexionRegistrada(Guid UsuarioId, HashSet<Guid> Conversaciones);
}
