using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Aprobar_pendiente_publica_UserVerified()
    {
        var usuarioId = Guid.NewGuid();
        var v = VerificacionKyc.Crear(usuarioId);
        var solicitudId = v.EnviarSolicitud("d", "s", TipoDocumento.CedulaIdentidad, DateTimeOffset.UnixEpoch).Valor.Id;
        var publicador = new PublicadorFake();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(v), publicador, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(solicitudId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(r.EsExito);
        var evento = Assert.Single(publicador.Publicados);
        Assert.Equal(usuarioId, Assert.IsType<UserVerified>(evento).UserId);
    }

    [Fact]
    public async Task Aprobar_solicitud_inexistente_no_publica()
    {
        var publicador = new PublicadorFake();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(null), publicador, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Empty(publicador.Publicados);
    }
}
