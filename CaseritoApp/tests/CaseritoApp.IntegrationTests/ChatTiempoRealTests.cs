using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using CaseritoApp.Host.Chat;
using CaseritoApp.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatTiempoRealTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Hub_rechaza_anonimo_y_acepta_bearer_valido()
    {
        await using var anonima = CrearConexion(null);
        await Assert.ThrowsAnyAsync<Exception>(() => anonima.StartAsync());
        var token = await RegistrarYObtenerTokenAsync();
        await using var autenticada = CrearConexion(token);

        await autenticada.StartAsync();

        Assert.Equal(HubConnectionState.Connected, autenticada.State);
    }

    [Fact]
    public async Task Conexion_autenticada_recibe_actualizacion_global_de_contador()
    {
        var token = await RegistrarYObtenerTokenAsync();
        var destinatarioId = UsuarioId(token);
        await using var conexion = CrearConexion(token);
        var recibido = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        conexion.On("ContadorChatActualizado", () => recibido.TrySetResult());
        await conexion.StartAsync();
        using var scope = factory.Services.CreateScope();
        var publicador = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publicador.Publish(new ChatMessageSent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            destinatarioId));

        await recibido.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Participante_se_suscribe_y_tercero_recibe_error_generico()
    {
        var tokenParticipante = await RegistrarYObtenerTokenAsync();
        var participanteId = UsuarioId(tokenParticipante);
        var conversacionId = await CrearConversacionAsync(participanteId);
        var tokenTercero = await RegistrarYObtenerTokenAsync();
        await using var participante = CrearConexion(tokenParticipante);
        await using var tercero = CrearConexion(tokenTercero);
        await participante.StartAsync();
        await tercero.StartAsync();

        await participante.InvokeAsync("SuscribirConversacion", conversacionId);
        var error = await Assert.ThrowsAsync<HubException>(() =>
            tercero.InvokeAsync("SuscribirConversacion", conversacionId));

        Assert.DoesNotContain(conversacionId.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(UsuarioId(tokenTercero).ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Identidad_sin_sub_valido_no_conecta()
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CaseritoApiFactory.JwtKeyDePrueba)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "CaseritoApp",
            audience: "CaseritoApp",
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credenciales);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        await using var conexion = CrearConexion(token);

        await Assert.ThrowsAnyAsync<Exception>(() => conexion.StartAsync());
    }

    [Fact]
    public async Task Conexion_rechaza_superar_el_maximo_de_conversaciones()
    {
        var token = await RegistrarYObtenerTokenAsync();
        var ids = await CrearConversacionesAsync(UsuarioId(token), 21);
        await using var conexion = CrearConexion(token);
        await conexion.StartAsync();
        foreach (var id in ids.Take(20))
        {
            await conexion.InvokeAsync("SuscribirConversacion", id);
        }

        var error = await Assert.ThrowsAsync<HubException>(() =>
            conexion.InvokeAsync("SuscribirConversacion", ids[20]));

        Assert.DoesNotContain(ids[20].ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mensaje_confirmado_se_publica_al_grupo_suscrito()
    {
        var token = await RegistrarYObtenerTokenAsync();
        var compradorId = UsuarioId(token);
        var conversacionId = await CrearConversacionAsync(compradorId);
        await using var conexion = CrearConexion(token);
        var recibido = new TaskCompletionSource<MensajeTiempoRealDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        conexion.On<MensajeTiempoRealDto>("MensajeCreado", mensaje => recibido.TrySetResult(mensaje));
        await conexion.StartAsync();
        await conexion.InvokeAsync("SuscribirConversacion", conversacionId);
        Guid mensajeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            var conversacion = await db.Conversaciones.FindAsync(conversacionId);
            var mensaje = conversacion!.CrearMensaje(
                compradorId, Guid.NewGuid(), 1, "Contenido", DateTimeOffset.UtcNow).Valor;
            mensajeId = mensaje.Id;
            db.Mensajes.Add(mensaje);
            await db.SaveChangesAsync();
        }

        var despachador = new DespachadorEntregasTiempoReal(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<IOptions<OpcionesTiempoRealChat>>());
        await despachador.ProcesarLoteAsync(CancellationToken.None);
        var mensajeRecibido = await recibido.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(mensajeId, mensajeRecibido.Id);
        Assert.Equal(conversacionId, mensajeRecibido.ConversacionId);
        Assert.Equal("Contenido", mensajeRecibido.Texto);
    }

    [Fact]
    public async Task Bloquear_retira_todas_las_conexiones_de_la_conversacion()
    {
        var token = await RegistrarYObtenerTokenAsync();
        var conversacionId = await CrearConversacionAsync(UsuarioId(token));
        await using var primera = CrearConexion(token);
        await using var segunda = CrearConexion(token);
        var primeraRecibio = 0;
        var segundaRecibio = 0;
        primera.On<MensajeTiempoRealDto>("MensajeCreado", _ => Interlocked.Increment(ref primeraRecibio));
        segunda.On<MensajeTiempoRealDto>("MensajeCreado", _ => Interlocked.Increment(ref segundaRecibio));
        await primera.StartAsync();
        await segunda.StartAsync();
        await primera.InvokeAsync("SuscribirConversacion", conversacionId);
        await segunda.InvokeAsync("SuscribirConversacion", conversacionId);
        using var scope = factory.Services.CreateScope();
        var publicador = scope.ServiceProvider.GetRequiredService<IPublicadorMensajesTiempoReal>();
        var mensaje = new MensajeTiempoRealDto(
            Guid.NewGuid(), conversacionId, Guid.NewGuid(), 1, "control", DateTimeOffset.UtcNow);
        await publicador.PublicarAsync(mensaje, CancellationToken.None);
        await EsperarAsync(() => Volatile.Read(ref primeraRecibio) == 1
            && Volatile.Read(ref segundaRecibio) == 1);
        using var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.PutAsync(
            $"/api/chat/conversaciones/{conversacionId}/bloqueo", null);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        await publicador.PublicarAsync(mensaje with { Id = Guid.NewGuid(), Secuencia = 2 }, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.Equal(1, Volatile.Read(ref primeraRecibio));
        Assert.Equal(1, Volatile.Read(ref segundaRecibio));
    }

    [Fact]
    public async Task Suscripcion_rechaza_conversacion_cerrada()
    {
        var token = await RegistrarYObtenerTokenAsync();
        var usuarioId = UsuarioId(token);
        var conversacionId = await CrearConversacionAsync(usuarioId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            var conversacion = await db.Conversaciones.FindAsync(conversacionId);
            Assert.True(conversacion!.CerrarPorParticipante(usuarioId, DateTimeOffset.UtcNow).EsExito);
            await db.SaveChangesAsync();
        }

        await using var conexion = CrearConexion(token);
        await conexion.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            conexion.InvokeAsync("SuscribirConversacion", conversacionId));
    }

    [Fact]
    public async Task Token_en_query_no_autentica_endpoints_http_ajenos_al_hub()
    {
        var token = await RegistrarYObtenerTokenAsync();
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync($"/api/perfil?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public void Hub_expone_solo_las_dos_invocaciones_aprobadas()
    {
        var metodos = typeof(ChatHub).GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(x => x.GetBaseDefinition().DeclaringType == typeof(ChatHub))
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(["DesuscribirConversacion", "SuscribirConversacion"], metodos);
    }

    [Fact]
    public async Task Solo_comprador_y_vendedor_pueden_acceder_a_la_conversacion()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        await db.SaveChangesAsync();
        var consulta = new ConsultaConversacionesEfCore(db);

        Assert.True(await consulta.PuedeAccederAsync(
            conversacion.Id, conversacion.CompradorId, CancellationToken.None));
        Assert.True(await consulta.PuedeAccederAsync(
            conversacion.Id, conversacion.VendedorId, CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            conversacion.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            Guid.NewGuid(), conversacion.CompradorId, CancellationToken.None));
        Assert.False(await consulta.PuedeAccederAsync(
            Guid.Empty, conversacion.CompradorId, CancellationToken.None));
    }

    [Fact]
    public void Grupo_de_conversacion_es_determinista_invariante_y_no_acepta_nombres()
    {
        var id = Guid.Parse("A48B63CD-4D4A-44E8-A3E2-D4F6A9E16051");

        var grupo = GruposChat.ParaConversacion(id);

        Assert.Equal("chat:conversacion:a48b63cd4d4a44e8a3e2d4f6a9e16051", grupo);
        Assert.Equal(2, typeof(GruposChat).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Length);
    }

    private HubConnection CrearConexion(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/chat"),
                opciones =>
                {
                    opciones.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    opciones.Transports = HttpTransportType.LongPolling;
                    if (token is not null)
                    {
                        opciones.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
            .Build();

    private async Task<string> RegistrarYObtenerTokenAsync()
    {
        using var cliente = factory.CreateClient();
        var email = $"chat-rt-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new CaseritoApp.Host.Endpoints.RegistroRequest(email, password, "Usuario", "Madrid"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new CaseritoApp.Host.Endpoints.LoginRequest(email, password));
        var body = await login.Content.ReadFromJsonAsync<CaseritoApp.Host.Endpoints.TokenAccesoResponse>();
        return body!.AccessToken;
    }

    private async Task<Guid> CrearConversacionAsync(Guid compradorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        await db.SaveChangesAsync();
        return conversacion.Id;
    }

    private async Task<Guid[]> CrearConversacionesAsync(Guid compradorId, int cantidad)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversaciones = Enumerable.Range(0, cantidad)
            .Select(_ => Conversacion.Crear(
                Guid.NewGuid(), compradorId, Guid.NewGuid(), DateTimeOffset.UtcNow).Valor)
            .ToArray();
        db.Conversaciones.AddRange(conversaciones);
        await db.SaveChangesAsync();
        return conversaciones.Select(x => x.Id).ToArray();
    }

    private static Guid UsuarioId(string token) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Subject);

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condicion() && DateTimeOffset.UtcNow < limite)
        {
            await Task.Delay(20);
        }

        Assert.True(condicion());
    }
}
