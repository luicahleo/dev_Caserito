using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.TiempoReal;

public sealed class AlmacenEntregasTiempoRealSql(
    ChatDbContext db,
    TimeProvider reloj) : IAlmacenEntregasTiempoReal
{
    private const int SegundosBackoffMaximo = 300;

    public async Task<IReadOnlyList<EntregaTiempoRealReclamada>> ReclamarAsync(
        int cantidadMaxima,
        TimeSpan duracionLease,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cantidadMaxima, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duracionLease, TimeSpan.Zero);

        var ahora = reloj.GetUtcNow();
        var leaseHasta = ahora.Add(duracionLease);
        var ids = await ReclamarIdsAsync(cantidadMaxima, ahora, leaseHasta, cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.EntregasTiempoReal
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.LeaseHasta == leaseHasta)
            .Join(
                db.Mensajes.AsNoTracking(),
                entrega => entrega.MensajeId,
                mensaje => mensaje.Id,
                (entrega, mensaje) => new { Entrega = entrega, Mensaje = mensaje })
            .OrderBy(x => x.Mensaje.Secuencia)
            .ThenBy(x => x.Entrega.Id)
            .Select(x => new EntregaTiempoRealReclamada(
                    x.Entrega.Id,
                    leaseHasta,
                    x.Entrega.Intentos,
                    new MensajeEntregaTiempoReal(
                        x.Mensaje.Id,
                        x.Mensaje.ConversacionId,
                        x.Mensaje.RemitenteId,
                        x.Mensaje.Secuencia,
                        x.Mensaje.Texto,
                        x.Mensaje.EnviadoEn)))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarcarProcesadaAsync(
        EntregaTiempoRealReclamada entrega,
        CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var actualizadas = await db.EntregasTiempoReal
            .Where(e => e.Id == entrega.EntregaId
                && e.ProcesadaEn == null
                && e.LeaseHasta == entrega.LeaseHasta
                && e.LeaseHasta >= ahora)
            .ExecuteUpdateAsync(
                cambios => cambios
                    .SetProperty(e => e.ProcesadaEn, ahora)
                    .SetProperty(e => e.LeaseHasta, (DateTimeOffset?)null),
                cancellationToken);
        return actualizadas == 1;
    }

    public async Task<bool> ReprogramarAsync(
        EntregaTiempoRealReclamada entrega,
        CancellationToken cancellationToken)
    {
        var ahora = reloj.GetUtcNow();
        var segundos = Math.Min(
            Math.Pow(2, Math.Min(entrega.Intentos + 1, 20)),
            SegundosBackoffMaximo);
        var proximoIntento = ahora.AddSeconds(segundos);
        var actualizadas = await db.EntregasTiempoReal
            .Where(e => e.Id == entrega.EntregaId
                && e.ProcesadaEn == null
                && e.LeaseHasta == entrega.LeaseHasta)
            .ExecuteUpdateAsync(
                cambios => cambios
                    .SetProperty(e => e.Intentos, e => e.Intentos + 1)
                    .SetProperty(e => e.ProximoIntentoEn, proximoIntento)
                    .SetProperty(e => e.LeaseHasta, (DateTimeOffset?)null),
                cancellationToken);
        return actualizadas == 1;
    }

    private async Task<List<Guid>> ReclamarIdsAsync(
        int cantidadMaxima,
        DateTimeOffset ahora,
        DateTimeOffset leaseHasta,
        CancellationToken cancellationToken)
    {
        var conexion = db.Database.GetDbConnection();
        var cerrarConexion = conexion.State != ConnectionState.Open;
        if (cerrarConexion)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
            await using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = """
                ;WITH candidatas AS
                (
                    SELECT TOP (@cantidadMaxima) *
                    FROM [chat].[EntregasTiempoReal] WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE [ProcesadaEn] IS NULL
                      AND [ProximoIntentoEn] <= @ahora
                      AND ([LeaseHasta] IS NULL OR [LeaseHasta] <= @ahora)
                    ORDER BY [Secuencia], [Id]
                )
                UPDATE candidatas
                SET [LeaseHasta] = @leaseHasta
                OUTPUT INSERTED.[Id];
                """;
            AgregarParametro(comando, "@cantidadMaxima", cantidadMaxima);
            AgregarParametro(comando, "@ahora", ahora);
            AgregarParametro(comando, "@leaseHasta", leaseHasta);

            var ids = new List<Guid>(cantidadMaxima);
            await using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
            {
                while (await lector.ReadAsync(cancellationToken))
                {
                    ids.Add(lector.GetGuid(0));
                }
            }

            await transaccion.CommitAsync(cancellationToken);
            return ids;
        }
        finally
        {
            if (cerrarConexion)
            {
                await conexion.CloseAsync();
            }
        }
    }

    private static void AgregarParametro(DbCommand comando, string nombre, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
