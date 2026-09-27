using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Paginacion;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ConsultasChatHandlerTests
{
    private sealed class ConversacionesFake : IConsultaConversaciones
    {
        public bool PuedeAcceder { get; init; }

        public Guid UsuarioId { get; private set; }

        public FronteraConversaciones? Frontera { get; private set; }

        public int Limite { get; private set; }

        public int NoLeidos { get; init; }

        public Task<PaginaCursor<ConversacionResumenDto, FronteraConversaciones>> ListarAsync(
            Guid usuarioId, FronteraConversaciones? frontera, int limite, CancellationToken ct)
        {
            UsuarioId = usuarioId;
            Frontera = frontera;
            Limite = limite;
            return Task.FromResult(new PaginaCursor<ConversacionResumenDto, FronteraConversaciones>([], null));
        }

        public Task<bool> PuedeAccederAsync(
            Guid conversacionId, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(PuedeAcceder);

        public Task<bool> PuedeRecibirTiempoRealAsync(
            Guid conversacionId, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(PuedeAcceder);

        public Task<int> ContarNoLeidosAsync(Guid usuarioId, CancellationToken ct)
        {
            UsuarioId = usuarioId;
            return Task.FromResult(NoLeidos);
        }
    }

    [Fact]
    public async Task PuedeAccederConversacion_devuelve_solo_la_decision_de_participacion()
    {
        var handler = new PuedeAccederConversacionQueryHandler(
            new ConversacionesFake { PuedeAcceder = true });

        var resultado = await handler.Handle(
            new PuedeAccederConversacionQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(resultado);
    }

    [Fact]
    public async Task PuedeRecibirTiempoReal_devuelve_la_decision_de_participacion()
    {
        var handler = new PuedeRecibirTiempoRealQueryHandler(
            new ConversacionesFake { PuedeAcceder = true });

        var resultado = await handler.Handle(
            new PuedeRecibirTiempoRealQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(resultado);
    }

    [Fact]
    public async Task ContarMensajesNoLeidos_devuelve_el_conteo_autoritativo()
    {
        var consulta = new ConversacionesFake { NoLeidos = 7 };
        var usuarioId = Guid.NewGuid();
        var handler = new ContarMensajesNoLeidosQueryHandler(consulta);

        var resultado = await handler.Handle(
            new ContarMensajesNoLeidosQuery(usuarioId), CancellationToken.None);

        Assert.Equal(7, resultado);
        Assert.Equal(usuarioId, consulta.UsuarioId);
    }

    private sealed class MensajesFake(
        PaginaCursor<MensajeDto, long>? pagina) : IConsultaMensajes
    {
        public Guid ConversacionId { get; private set; }

        public Guid UsuarioId { get; private set; }

        public long? AntesDe { get; private set; }

        public long? DespuesDe { get; private set; }

        public int Limite { get; private set; }

        public Task<PaginaCursor<MensajeDto, long>?> ListarAsync(
            Guid conversacionId,
            Guid usuarioId,
            long? antesDe,
            long? despuesDe,
            int limite,
            CancellationToken ct)
        {
            ConversacionId = conversacionId;
            UsuarioId = usuarioId;
            AntesDe = antesDe;
            DespuesDe = despuesDe;
            Limite = limite;
            return Task.FromResult(pagina);
        }
    }

    [Fact]
    public async Task ListarConversaciones_delega_usuario_frontera_y_limite()
    {
        var consulta = new ConversacionesFake();
        var handler = new ListarConversacionesQueryHandler(consulta);
        var usuarioId = Guid.NewGuid();
        var frontera = new FronteraConversaciones(
            new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero), Guid.NewGuid());

        await handler.Handle(new ListarConversacionesQuery(usuarioId, frontera, 25), CancellationToken.None);

        Assert.Equal(usuarioId, consulta.UsuarioId);
        Assert.Equal(frontera, consulta.Frontera);
        Assert.Equal(25, consulta.Limite);
    }

    [Fact]
    public async Task ObtenerMensajes_delega_frontera_y_devuelve_pagina()
    {
        var pagina = new PaginaCursor<MensajeDto, long>([], 50);
        var consulta = new MensajesFake(pagina);
        var handler = new ObtenerMensajesQueryHandler(consulta);
        var conversacionId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var resultado = await handler.Handle(
            new ObtenerMensajesQuery(conversacionId, usuarioId, 100, null, 40), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Same(pagina, resultado.Valor);
        Assert.Equal(conversacionId, consulta.ConversacionId);
        Assert.Equal(usuarioId, consulta.UsuarioId);
        Assert.Equal(100, consulta.AntesDe);
        Assert.Null(consulta.DespuesDe);
        Assert.Equal(40, consulta.Limite);
    }

    [Fact]
    public async Task ObtenerMensajes_delega_frontera_hacia_delante()
    {
        var pagina = new PaginaCursor<MensajeDto, long>([], null);
        var consulta = new MensajesFake(pagina);
        var handler = new ObtenerMensajesQueryHandler(consulta);

        var resultado = await handler.Handle(
            new ObtenerMensajesQuery(Guid.NewGuid(), Guid.NewGuid(), null, 25, 40),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Null(consulta.AntesDe);
        Assert.Equal(25, consulta.DespuesDe);
        Assert.Equal(40, consulta.Limite);
    }

    [Fact]
    public async Task ObtenerMensajes_no_distingue_ausencia_de_no_participante()
    {
        var handler = new ObtenerMensajesQueryHandler(new MensajesFake(null));

        var resultado = await handler.Handle(
            new ObtenerMensajesQuery(Guid.NewGuid(), Guid.NewGuid(), null, null, 50), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal("chat_conversacion_no_encontrada", resultado.Error.Code);
    }

    [Fact]
    public void Validators_aplican_limites_y_campos_obligatorios()
    {
        var conversaciones = new ListarConversacionesQueryValidator().Validate(
            new ListarConversacionesQuery(Guid.Empty, null, 51));
        var mensajes = new ObtenerMensajesQueryValidator().Validate(
            new ObtenerMensajesQuery(Guid.Empty, Guid.Empty, 0, -1, 101));

        Assert.False(conversaciones.IsValid);
        Assert.False(mensajes.IsValid);
        Assert.Contains(conversaciones.Errors, e => e.PropertyName == nameof(ListarConversacionesQuery.Limite));
        Assert.Contains(mensajes.Errors, e => e.PropertyName == nameof(ObtenerMensajesQuery.AntesDeSecuencia));
        Assert.Contains(mensajes.Errors, e => e.PropertyName == nameof(ObtenerMensajesQuery.DespuesDeSecuencia));
    }

    [Fact]
    public void ContarMensajesNoLeidos_rechaza_usuario_vacio()
    {
        var resultado = new ContarMensajesNoLeidosQueryValidator().Validate(
            new ContarMensajesNoLeidosQuery(Guid.Empty));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(ContarMensajesNoLeidosQuery.UsuarioId));
    }

    [Fact]
    public void ObtenerMensajes_rechaza_dos_fronteras_simultaneas()
    {
        var resultado = new ObtenerMensajesQueryValidator().Validate(
            new ObtenerMensajesQuery(Guid.NewGuid(), Guid.NewGuid(), 10, 20, 50));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "Los parámetros de paginación son incompatibles.");
    }
}
