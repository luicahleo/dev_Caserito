using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class EnviarSolicitudKycCommandHandlerTests
{
    private sealed class AlmacenFake : IAlmacenBlobsKyc
    {
        public int Guardados { get; private set; }
        public int Eliminados { get; private set; }

        public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult($"clave-{Guardados}");
        }

        public Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct) =>
            Task.FromResult(new BlobKyc([], "image/png"));

        public Task EliminarAsync(string clave, CancellationToken ct)
        {
            Eliminados++;
            return Task.CompletedTask;
        }
    }

    private sealed class RepoFake(VerificacionKyc? existente) : IRepositorioVerificacionKyc
    {
        public VerificacionKyc? Agregada { get; private set; }

        public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(existente);
        public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
            Task.FromResult<VerificacionKyc?>(null);
        public void Agregar(VerificacionKyc verificacion) => Agregada = verificacion;
        public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
            EstadoKyc? estado, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<SolicitudKycResumenDto>([], pagina, tamano, 0));
    }

    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Envio_nuevo_guarda_dos_blobs_y_agrega_agregado()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente: null);
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(Guid.NewGuid(), _png, "image/png", _png, "image/png"),
            CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(2, almacen.Guardados);
        Assert.Equal(0, almacen.Eliminados);
        Assert.NotNull(repo.Agregada);
    }

    [Fact]
    public async Task Envio_con_pendiente_existente_borra_los_blobs_escritos()
    {
        var usuarioId = Guid.NewGuid();
        var existente = VerificacionKyc.Crear(usuarioId);
        existente.EnviarSolicitud("d", "s", TipoDocumento.CedulaIdentidad, DateTimeOffset.UnixEpoch);

        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente);
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(usuarioId, _png, "image/png", _png, "image/png"),
            CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
        Assert.Equal(2, almacen.Guardados);
        Assert.Equal(2, almacen.Eliminados);
    }
}
