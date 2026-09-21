using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using MediatR;
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

    private sealed class OpcionesFake(double umbral) : IOpcionesResolucionKyc
    {
        public double UmbralAutoAprobacionSimilitud => umbral;
    }

    private sealed class PublicadorFake
        : IPublicadorEventosIntegracion, IProtectorDocumentoKyc, IPublisher
    {
        public List<IIntegrationEvent> Eventos { get; } = [];

        public List<object> Notificaciones { get; } = [];

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

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notificaciones.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notificaciones.Add(notification!);
            return Task.CompletedTask;
        }
    }

    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static EnviarSolicitudKycCommand ComandoValido() =>
        new(Guid.NewGuid(), [1, 2, 3], "image/png", [4, 5, 6], "image/png");

    private static EnviarSolicitudKycCommandHandler CrearHandler(
        RepoFake repo,
        AlmacenFake almacen,
        VerificadorFake verificador,
        PublicadorFake publicador,
        IOpcionesResolucionKyc opciones) =>
        // PublicadorFake cubre tres puertos: protector de documento, publicador de
        // integración y IPublisher. Por eso aparece tres veces seguidas.
        new(repo, almacen, verificador, publicador, publicador, publicador, opciones,
            TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

    [Fact]
    public async Task Envio_nuevo_guarda_dos_blobs_y_agrega_agregado()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente: null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(new VerificacionFacialResultado(true, 95.0, null)));
        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));

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
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(new VerificacionFacialResultado(true, 95.0, null)));
        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(usuarioId, _png, "image/png", _png, "image/png"),
            CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
        Assert.Equal(0, almacen.Guardados);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Score_alto_aprueba_y_publica_los_dos_eventos()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 90, MotivoRechazo: null)));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoKyc.Aprobada, repo.Agregada!.Solicitudes.Single().Estado);
        Assert.Equal(SistemaActor.Id, repo.Agregada.Solicitudes.Single().ResueltaPor);
        Assert.Contains(publicador.Eventos, e => e is UserVerified);
        Assert.Contains(publicador.Notificaciones, n => n is KycResuelto);
    }

    [Fact]
    public async Task Score_bajo_deja_pendiente_con_motivo_y_no_publica()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 40, MotivoRechazo: null)));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        var solicitud = repo.Agregada!.Solicitudes.Single();
        Assert.Equal(EstadoKyc.Pendiente, solicitud.Estado);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, solicitud.MotivoRevision);
        Assert.DoesNotContain(publicador.Eventos, e => e is UserVerified);
    }

    [Fact]
    public async Task Argos_caido_deja_pendiente_conserva_blobs_y_devuelve_exito()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Fallo<VerificacionFacialResultado>(
            new Error(ErroresKyc.ServicioVerificacionNoDisponible, "No disponible")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        var solicitud = repo.Agregada!.Solicitudes.Single();
        Assert.Equal(EstadoKyc.Pendiente, solicitud.Estado);
        Assert.Equal(MotivoRevisionKyc.ServicioNoDisponible, solicitud.MotivoRevision);
        Assert.Null(solicitud.ScoreSimilitud);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Rostro_no_detectado_deja_pendiente_con_su_motivo()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Fallo<VerificacionFacialResultado>(
            new Error(ErroresKyc.RostroNoDetectado, "No se detecto rostro")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(
            MotivoRevisionKyc.RostroNoDetectado,
            repo.Agregada!.Solicitudes.Single().MotivoRevision);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Sin_coincidencia_rechaza_y_no_publica_verificacion()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: false, SimilitudPercent: 10, MotivoRechazo: "no coincide")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoKyc.Rechazada, repo.Agregada!.Solicitudes.Single().Estado);
        Assert.DoesNotContain(publicador.Eventos, e => e is UserVerified);
    }
}
