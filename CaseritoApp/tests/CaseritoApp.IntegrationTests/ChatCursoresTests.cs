using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Host.Chat;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatCursoresTests
{
    [Fact]
    public void Cursor_conversaciones_roundtrip()
    {
        var frontera = new FronteraConversaciones(
            new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        var cursor = CursoresChat.Codificar(frontera);
        var valido = CursoresChat.TryDecodificarConversaciones(cursor, out var decodificada);

        Assert.True(valido);
        Assert.Equal(frontera, decodificada);
    }

    [Fact]
    public void Cursor_mensajes_roundtrip()
    {
        var cursor = CursoresChat.Codificar(123L);

        var valido = CursoresChat.TryDecodificarMensajes(cursor, out var secuencia);

        Assert.True(valido);
        Assert.Equal(123, secuencia);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-base64***")]
    [InlineData("AQ")]
    public void Cursores_invalidos_se_rechazan_sin_excepcion(string cursor)
    {
        Assert.False(CursoresChat.TryDecodificarConversaciones(cursor, out _));
        Assert.False(CursoresChat.TryDecodificarMensajes(cursor, out _));
    }
}
