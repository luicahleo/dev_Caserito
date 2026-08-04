using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.BuildingBlocks.Domain;
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

    private sealed class VerificadorFake(Result<VerificacionFacialResultado> resultado) : IVerificadorIdentidadArgos
    {
        public Task<Result<VerificacionFacialResultado>> VerificarAsync(
            byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct) =>
            Task.FromResult(resultado);
    }

    private sealed class PublicadorFake : IPublicadorEventosIntegracion, IProtectorDocumentoKyc
    {
        public List<IIntegrationEvent> Eventos { get; } = [];

        public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public DocumentoKycProtegido Proteger(
            string numeroCi,
            string? complementoCi,
            DepartamentoBolivia departamentoExpedicion) =>
            new("HUELLA", "CIFRADO", null, departamentoExpedicion);

        public string Descifrar(string valorCifrado) => "1234567";
    }

    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Envio_nuevo_guarda_dos_blobs_y_agrega_agregado()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente: null);
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, new VerificadorFake(Result.Exito(new VerificacionFacialResultado(true, 95.0, null))), new PublicadorFake(), TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

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
            repo, almacen, new VerificadorFake(Result.Exito(new VerificacionFacialResultado(true, 95.0, null))), new PublicadorFake(), TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(usuarioId, _png, "image/png", _png, "image/png"),
            CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
        Assert.Equal(0, almacen.Guardados);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Envio_con_coincidencia_deja_pendiente_para_revision_manual()
    {
        var usuarioId = Guid.NewGuid();
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var verificador = new VerificadorFake(Result.Exito(new VerificacionFacialResultado(true, 92.5, null)));
        var publicador = new PublicadorFake();
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, verificador, publicador, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new EnviarSolicitudKycCommand(
            usuarioId, _png, "image/png", _png, "image/png"), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Empty(publicador.Eventos);
        Assert.Equal(EstadoKyc.Pendiente, repo.Agregada!.SolicitudActual!.Estado);
        Assert.Equal(92.5, repo.Agregada.Solicitudes.Single().ScoreSimilitud);
    }

    [Fact]
    public async Task Envio_sin_coincidencia_automatica_rechaza_sin_publicar_evento()
    {
        var usuarioId = Guid.NewGuid();
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var verificador = new VerificadorFake(Result.Exito(new VerificacionFacialResultado(false, 32.0, "El rostro no coincide")));
        var publicador = new PublicadorFake();
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, verificador, publicador, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new EnviarSolicitudKycCommand(
            usuarioId, _png, "image/png", _png, "image/png"), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Empty(publicador.Eventos);
        Assert.Equal(EstadoKyc.Rechazada, repo.Agregada!.Solicitudes.Single().Estado);
    }

    [Fact]
    public async Task Envio_con_argos_caido_compensa_blobs_y_devuelve_ServicioVerificacionNoDisponible()
    {
        var usuarioId = Guid.NewGuid();
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var verificador = new VerificadorFake(Result.Fallo<VerificacionFacialResultado>(
            new Error(ErroresKyc.ServicioVerificacionNoDisponible, "No disponible")));
        var publicador = new PublicadorFake();
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, verificador, publicador, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new EnviarSolicitudKycCommand(
            usuarioId, _png, "image/png", _png, "image/png"), CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.ServicioVerificacionNoDisponible, r.Error.Code);
        Assert.Equal(2, almacen.Guardados);
        Assert.Equal(2, almacen.Eliminados);
        Assert.Null(repo.Agregada);
    }
}
