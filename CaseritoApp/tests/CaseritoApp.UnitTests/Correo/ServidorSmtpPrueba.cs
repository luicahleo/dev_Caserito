using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CaseritoApp.UnitTests.Correo;

internal sealed class ServidorSmtpPrueba : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cancelacion = new();
    private readonly Task _servidor;
    private readonly bool _anunciarStartTls;

    public ServidorSmtpPrueba(bool anunciarStartTls = false)
    {
        _anunciarStartTls = anunciarStartTls;
        _listener.Start();
        Puerto = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _servidor = AtenderAsync(_cancelacion.Token);
    }

    public int Puerto { get; }

    public List<string> Comandos { get; } = [];

    private async Task AtenderAsync(CancellationToken ct)
    {
        using var cliente = await _listener.AcceptTcpClientAsync(ct);
        await using var stream = cliente.GetStream();
        using var lector = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await ResponderAsync(stream, "220 smtp.test ESMTP listo\r\n", ct);

        var leyendoDatos = false;
        while (await lector.ReadLineAsync(ct) is { } linea)
        {
            if (leyendoDatos)
            {
                if (linea == ".")
                {
                    leyendoDatos = false;
                    await ResponderAsync(stream, "250 2.0.0 aceptado\r\n", ct);
                }

                continue;
            }

            Comandos.Add(linea);
            var comando = linea.Split(' ', 2)[0].ToUpperInvariant();
            switch (comando)
            {
                case "EHLO":
                    var capacidades = _anunciarStartTls
                        ? "250-smtp.test\r\n250-STARTTLS\r\n250 OK\r\n"
                        : "250-smtp.test\r\n250 OK\r\n";
                    await ResponderAsync(stream, capacidades, ct);
                    break;
                case "HELO":
                case "MAIL":
                case "RCPT":
                    await ResponderAsync(stream, "250 OK\r\n", ct);
                    break;
                case "DATA":
                    leyendoDatos = true;
                    await ResponderAsync(stream, "354 terminar con punto\r\n", ct);
                    break;
                case "STARTTLS":
                    await ResponderAsync(stream, "220 iniciar TLS\r\n", ct);
                    return;
                case "QUIT":
                    await ResponderAsync(stream, "221 adios\r\n", ct);
                    return;
            }
        }
    }

    private static Task ResponderAsync(NetworkStream stream, string respuesta, CancellationToken ct)
        => stream.WriteAsync(Encoding.ASCII.GetBytes(respuesta), ct).AsTask();

    public async ValueTask DisposeAsync()
    {
        await _cancelacion.CancelAsync();
        _listener.Stop();
        try
        {
            await _servidor;
        }
        catch (OperationCanceledException ex)
        {
            _ = ex;
        }

        _cancelacion.Dispose();
    }
}
