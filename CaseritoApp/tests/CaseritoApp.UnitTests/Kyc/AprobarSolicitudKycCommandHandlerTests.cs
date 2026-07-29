using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class AprobarSolicitudKycCommandHandlerTests
{
    private sealed class PublicadorFake : IPublicadorEventosIntegracion
    {
        public List<IIntegrationEvent> Publicados { get; } = [];
        public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
        {
            Publicados.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class RepoFake(VerificacionKyc? verificacion) : IRepositorioVerificacionKyc
    {
        public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(verificacion);
        public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
            Task.FromResult(verificacion);
        public void Agregar(VerificacionKyc verificacion) { }
        public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
            EstadoKyc? estado, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<SolicitudKycResumenDto>([], pagina, tamano, 0));
    }

    [Fact]
    public async Task Aprobar_pendiente_publica_UserVerified_y_KycResuelto()
    {
        var usuarioId = Guid.NewGuid();
        var v = VerificacionKyc.Crear(usuarioId);
        var solicitudId = v.EnviarSolicitud("d", "s", TipoDocumento.CedulaIdentidad, DateTimeOffset.UnixEpoch).Valor.Id;
        var publicador = new PublicadorFake();
        var publisher = Substitute.For<IPublisher>();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(v), publicador, publisher, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(solicitudId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(r.EsExito);
        var evento = Assert.Single(publicador.Publicados);
        Assert.Equal(usuarioId, Assert.IsType<UserVerified>(evento).UserId);
        await publisher.Received(1).Publish(
            Arg.Is<KycResuelto>(e => e.UsuarioId == usuarioId && e.SolicitudId == solicitudId && e.Estado == EstadoKyc.Aprobada),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aprobar_solicitud_inexistente_no_publica()
    {
        var publicador = new PublicadorFake();
        var publisher = Substitute.For<IPublisher>();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(null), publicador, publisher, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Empty(publicador.Publicados);
        await publisher.DidNotReceive().Publish(Arg.Any<KycResuelto>(), Arg.Any<CancellationToken>());
    }
}
