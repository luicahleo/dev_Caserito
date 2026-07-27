using CaseritoApp.BuildingBlocks.Contracts.Catalog;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Application.Busquedas;
using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Domain.Busquedas;
using CaseritoApp.Notifications.Domain.Notificaciones;
using MediatR;
using NSubstitute;

namespace CaseritoApp.UnitTests.Notifications;

public sealed class NotificarProductoPublicadoHandlerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IBusquedaGuardadaRepository _busquedas = Substitute.For<IBusquedaGuardadaRepository>();
    private readonly IConsultaProductoParaAlerta _consulta = Substitute.For<IConsultaProductoParaAlerta>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IConsultaEmailUsuario _consultaEmail = Substitute.For<IConsultaEmailUsuario>();

    private NotificarProductoPublicadoHandler CrearHandler() => new(
        _sender,
        _busquedas,
        _consulta,
        _emailSender,
        _consultaEmail);

    [Fact]
    public async Task Handle_ProductoNoEncontrado_NoEnviaNada()
    {
        var evento = new ProductPublished(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        _consulta.ObtenerAsync(evento.ProductId, Arg.Any<CancellationToken>()).Returns((ProductoAlertaDto?)null);

        await CrearHandler().Handle(evento, CancellationToken.None);

        await _busquedas.DidNotReceive().ListarCoincidenciasAsync(
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<decimal?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<CrearNotificacionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Coincidencias_CreaNotificacionYEnviaEmail()
    {
        var evento = new ProductPublished(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var usuarioId = Guid.NewGuid();
        var producto = new ProductoAlertaDto(
            evento.ProductId,
            "iPhone 14",
            "tecnología",
            "cochabamba",
            3500,
            "usado");

        _consulta.ObtenerAsync(evento.ProductId, Arg.Any<CancellationToken>()).Returns(producto);
        _busquedas.ListarCoincidenciasAsync(
                producto.Titulo,
                producto.Categoria,
                producto.Ciudad,
                producto.Precio,
                producto.EstadoProducto,
                Arg.Any<CancellationToken>())
            .Returns(new List<BusquedaGuardada>
            {
                CrearBusqueda(usuarioId, "iphone", "tecnología", "cochabamba"),
            });
        _sender.Send(Arg.Any<CrearNotificacionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Exito(Guid.NewGuid()));
        _consultaEmail.ObtenerEmailAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns("usuario@caserito.test");

        await CrearHandler().Handle(evento, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CrearNotificacionCommand>(c =>
                c.DestinatarioId == usuarioId
                && c.Tipo == TipoNotificacion.AlertaBusqueda
                && c.EntidadRelacionadaId == evento.ProductId),
            Arg.Any<CancellationToken>());
        await _emailSender.Received(1).EnviarAsync(
            "usuario@caserito.test",
            Arg.Any<string>(),
            Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DosBusquedasDelMismoUsuario_GeneranDosAlertas()
    {
        var evento = new ProductPublished(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var usuarioId = Guid.NewGuid();
        var producto = new ProductoAlertaDto(evento.ProductId, "iPhone 14", null, null, null, null);

        _consulta.ObtenerAsync(evento.ProductId, Arg.Any<CancellationToken>()).Returns(producto);
        _busquedas.ListarCoincidenciasAsync(
                producto.Titulo,
                producto.Categoria,
                producto.Ciudad,
                producto.Precio,
                producto.EstadoProducto,
                Arg.Any<CancellationToken>())
            .Returns(new List<BusquedaGuardada>
            {
                CrearBusqueda(usuarioId, "iphone", null, null),
                CrearBusqueda(usuarioId, "apple", null, null),
            });
        _sender.Send(Arg.Any<CrearNotificacionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Exito(Guid.NewGuid()));
        _consultaEmail.ObtenerEmailAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns("usuario@caserito.test");

        await CrearHandler().Handle(evento, CancellationToken.None);

        await _sender.Received(2).Send(
            Arg.Is<CrearNotificacionCommand>(c =>
                c.DestinatarioId == usuarioId && c.Tipo == TipoNotificacion.AlertaBusqueda),
            Arg.Any<CancellationToken>());
        await _emailSender.Received(2).EnviarAsync(
            "usuario@caserito.test",
            Arg.Any<string>(),
            Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FalloDeEmail_NoPropagaError()
    {
        var evento = new ProductPublished(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var usuarioId = Guid.NewGuid();
        var producto = new ProductoAlertaDto(evento.ProductId, "iPhone 14", null, null, null, null);

        _consulta.ObtenerAsync(evento.ProductId, Arg.Any<CancellationToken>()).Returns(producto);
        _busquedas.ListarCoincidenciasAsync(
                producto.Titulo,
                producto.Categoria,
                producto.Ciudad,
                producto.Precio,
                producto.EstadoProducto,
                Arg.Any<CancellationToken>())
            .Returns(new List<BusquedaGuardada> { CrearBusqueda(usuarioId, "iphone", null, null) });
        _sender.Send(Arg.Any<CrearNotificacionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Exito(Guid.NewGuid()));
        _consultaEmail.ObtenerEmailAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns("usuario@caserito.test");
        _emailSender.EnviarAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Servicio de email caído")));

        var ex = await Record.ExceptionAsync(() => CrearHandler().Handle(evento, CancellationToken.None));

        Assert.Null(ex);
        await _sender.Received(1).Send(Arg.Any<CrearNotificacionCommand>(), Arg.Any<CancellationToken>());
    }

    private static BusquedaGuardada CrearBusqueda(
        Guid usuarioId,
        string? palabraClave,
        string? categoria,
        string? ciudad)
    {
        return BusquedaGuardada.Crear(
            usuarioId,
            palabraClave,
            categoria,
            ciudad,
            null,
            null,
            null,
            DateTimeOffset.UtcNow).Valor;
    }
}
