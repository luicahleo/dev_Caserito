using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using CaseritoApp.Chat.Infrastructure.Mensajes;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatPersistenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly DateTimeOffset _ahora = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Modelo_configura_restricciones_secuencia_y_fk_solo_interna()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = db.Model.FindEntityType(typeof(Conversacion))!;
        var mensaje = db.Model.FindEntityType(typeof(Mensaje))!;

        Assert.Equal("chat", conversacion.GetSchema());
        Assert.Contains(conversacion.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(Conversacion.CompradorId), nameof(Conversacion.AvisoId)]));
        Assert.Contains(mensaje.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([
                nameof(Mensaje.ConversacionId), nameof(Mensaje.RemitenteId), nameof(Mensaje.ClaveIdempotencia)]));
        Assert.Single(mensaje.GetForeignKeys());
        Assert.Equal(typeof(Conversacion), mensaje.GetForeignKeys().Single().PrincipalEntityType.ClrType);
        Assert.True(conversacion.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Contains(db.Model.GetSequences(), s => s.Name == "SecuenciaMensajes" && s.Schema == "chat");
    }

    [Fact]
    public async Task Restriccion_unica_impide_dos_conversaciones_del_mismo_comprador_y_aviso()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var compradorId = Guid.NewGuid();
        var avisoId = Guid.NewGuid();
        db.Conversaciones.Add(NuevaConversacion(avisoId, compradorId));
        db.Conversaciones.Add(NuevaConversacion(avisoId, compradorId));

        var excepcion = await Assert.ThrowsAsync<ConflictoUnicidadChatException>(
            () => new UnitOfWorkChat(db).GuardarCambiosAsync(CancellationToken.None));
        Assert.Null(excepcion.InnerException);
    }

    [Fact]
    public async Task Reserva_secuencias_crecientes_y_restringe_clave_idempotente()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = NuevaConversacion();
        db.Conversaciones.Add(conversacion);
        await db.SaveChangesAsync();
        var repositorio = new RepositorioMensajesEfCore(db);
        var primera = await repositorio.ReservarSecuenciaAsync(CancellationToken.None);
        var segunda = await repositorio.ReservarSecuenciaAsync(CancellationToken.None);
        var clave = Guid.NewGuid();
        var mensajeA = conversacion.CrearMensaje(
            conversacion.CompradorId, clave, primera, "Uno", _ahora.AddMinutes(1)).Valor;
        repositorio.Agregar(mensajeA);
        await db.SaveChangesAsync();

        var mensajeB = conversacion.CrearMensaje(
            conversacion.CompradorId, clave, segunda, "Duplicado", _ahora.AddMinutes(2)).Valor;
        db.Mensajes.Add(mensajeB);

        Assert.True(segunda > primera);
        var excepcion = await Assert.ThrowsAsync<ConflictoUnicidadChatException>(
            () => new UnitOfWorkChat(db).GuardarCambiosAsync(CancellationToken.None));
        Assert.Null(excepcion.InnerException);
    }

    [Fact]
    public async Task Consulta_mensajes_autoriza_y_pagina_hacia_atras_en_orden_cronologico()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = NuevaConversacion();
        db.Conversaciones.Add(conversacion);
        for (var secuencia = 1; secuencia <= 4; secuencia++)
        {
            db.Mensajes.Add(conversacion.CrearMensaje(
                conversacion.CompradorId,
                Guid.NewGuid(),
                secuencia,
                $"Mensaje {secuencia}",
                _ahora.AddMinutes(secuencia)).Valor);
        }

        await db.SaveChangesAsync();
        var consulta = new ConsultaMensajesEfCore(db);

        var pagina = await consulta.ListarAsync(
            conversacion.Id, conversacion.VendedorId, 4, 2, CancellationToken.None);
        var tercero = await consulta.ListarAsync(
            conversacion.Id, Guid.NewGuid(), null, 2, CancellationToken.None);

        Assert.Equal([2L, 3L], pagina!.Items.Select(m => m.Secuencia));
        Assert.Equal(2, pagina.Siguiente);
        Assert.Null(tercero);
    }

    [Fact]
    public async Task Consulta_conversaciones_aisla_usuario_y_calcula_no_leidos_de_la_contraparte()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var compradorId = Guid.NewGuid();
        var propia = NuevaConversacion(compradorId: compradorId);
        var ajena = NuevaConversacion();
        db.Conversaciones.AddRange(propia, ajena);
        db.Mensajes.Add(propia.CrearMensaje(
            propia.VendedorId, Guid.NewGuid(), 1, "Respuesta", _ahora.AddMinutes(1)).Valor);
        db.Mensajes.Add(propia.CrearMensaje(
            propia.CompradorId, Guid.NewGuid(), 2, "Gracias", _ahora.AddMinutes(2)).Valor);
        await db.SaveChangesAsync();
        var consulta = new ConsultaConversacionesEfCore(db);

        var pagina = await consulta.ListarAsync(compradorId, null, 20, CancellationToken.None);

        var item = Assert.Single(pagina.Items);
        Assert.Equal(propia.Id, item.Id);
        Assert.Equal(1, item.NoLeidos);
        Assert.Equal(propia.VendedorId, item.ContraparteId);
        Assert.Equal("Comprador", item.Rol);
    }

    [Fact]
    public async Task Cursor_conversaciones_no_duplica_ni_omite_con_fechas_empatadas()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var compradorId = Guid.NewGuid();
        var conversaciones = Enumerable.Range(0, 3)
            .Select(_ => NuevaConversacion(compradorId: compradorId))
            .ToArray();
        db.Conversaciones.AddRange(conversaciones);
        await db.SaveChangesAsync();
        var consulta = new ConsultaConversacionesEfCore(db);

        var primera = await consulta.ListarAsync(compradorId, null, 2, CancellationToken.None);
        var segunda = await consulta.ListarAsync(
            compradorId, primera.Siguiente, 2, CancellationToken.None);

        Assert.NotNull(primera.Siguiente);
        Assert.Equal(3, primera.Items.Concat(segunda.Items).Select(c => c.Id).Distinct().Count());
        Assert.Null(segunda.Siguiente);
    }

    [Fact]
    public async Task Rowversion_detecta_actualizaciones_concurrentes_de_lectura()
    {
        Guid conversacionId;
        Guid compradorId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            var conversacion = NuevaConversacion();
            conversacionId = conversacion.Id;
            compradorId = conversacion.CompradorId;
            db.Conversaciones.Add(conversacion);
            db.Mensajes.Add(conversacion.CrearMensaje(
                conversacion.VendedorId, Guid.NewGuid(), 2, "Hola", _ahora.AddMinutes(1)).Valor);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ChatDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacionA = await dbA.Conversaciones.SingleAsync(c => c.Id == conversacionId);
        var conversacionB = await dbB.Conversaciones.SingleAsync(c => c.Id == conversacionId);
        conversacionA.MarcarLectura(compradorId, 1, _ahora.AddMinutes(2));
        conversacionB.MarcarLectura(compradorId, 2, _ahora.AddMinutes(2));

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    private static Conversacion NuevaConversacion(
        Guid? avisoId = null,
        Guid? compradorId = null) => Conversacion.Crear(
            avisoId ?? Guid.NewGuid(),
            compradorId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            _ahora).Valor;

}
