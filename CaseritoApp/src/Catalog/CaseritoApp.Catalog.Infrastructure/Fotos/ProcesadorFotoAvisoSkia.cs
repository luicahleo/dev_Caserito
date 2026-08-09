using CaseritoApp.Catalog.Application.Fotos;
using SkiaSharp;

namespace CaseritoApp.Catalog.Infrastructure.Fotos;

/// <summary>Normaliza fotos con Skia sin conservar metadata.</summary>
public sealed class ProcesadorFotoAvisoSkia : IProcesadorFotoAviso
{
    private const int LadoMaximo = 1600;
    private const int LadoMinimo = 480;
    private const int LimiteSalidaBytes = 1024 * 1024;
    private static readonly int[] _calidades = [82, 74, 66, 58, 50, 42];

    /// <inheritdoc />
    public Task<FotoAvisoProcesada?> ProcesarAsync(
        byte[] contenido,
        string contentType,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var datos = SKData.CreateCopy(contenido);
            using var codec = SKCodec.Create(datos);
            if (codec is null)
            {
                return Task.FromResult<FotoAvisoProcesada?>(null);
            }

            using var decodificada = SKBitmap.Decode(codec);
            if (decodificada is null || decodificada.Width <= 0 || decodificada.Height <= 0)
            {
                return Task.FromResult<FotoAvisoProcesada?>(null);
            }

            using var orientada = AplicarOrientacion(decodificada, codec.EncodedOrigin);
            var escalaInicial = Math.Min(1d, (double)LadoMaximo / Math.Max(orientada.Width, orientada.Height));
            var ancho = Math.Max(1, (int)Math.Round(orientada.Width * escalaInicial));
            var alto = Math.Max(1, (int)Math.Round(orientada.Height * escalaInicial));

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                using var renderizada = Renderizar(orientada, ancho, alto);
                using var imagen = SKImage.FromBitmap(renderizada);
                foreach (var calidad in _calidades)
                {
                    using var salida = imagen.Encode(SKEncodedImageFormat.Jpeg, calidad);
                    if (salida.Size <= LimiteSalidaBytes)
                    {
                        return Task.FromResult<FotoAvisoProcesada?>(
                            new(salida.ToArray(), "image/jpeg"));
                    }
                }

                if (Math.Max(ancho, alto) <= LadoMinimo)
                {
                    return Task.FromResult<FotoAvisoProcesada?>(null);
                }

                var escala = Math.Max(0.5d, (double)LadoMinimo / Math.Max(ancho, alto));
                ancho = Math.Max(1, (int)Math.Round(ancho * escala));
                alto = Math.Max(1, (int)Math.Round(alto * escala));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult<FotoAvisoProcesada?>(null);
        }
    }

    private static SKBitmap Renderizar(SKBitmap origen, int ancho, int alto)
    {
        var salida = new SKBitmap(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(salida);
        canvas.Clear(SKColors.White);
        using var pintura = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(
            origen,
            new SKRect(0, 0, ancho, alto),
            new SKSamplingOptions(SKFilterMode.Linear),
            pintura);
        return salida;
    }

    private static SKBitmap AplicarOrientacion(SKBitmap origen, SKEncodedOrigin orientacion)
    {
        var intercambiaEjes = orientacion is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
        var ancho = intercambiaEjes ? origen.Height : origen.Width;
        var alto = intercambiaEjes ? origen.Width : origen.Height;
        var salida = new SKBitmap(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(salida);
        var matriz = MatrizOrientacion(orientacion, origen.Width, origen.Height);
        canvas.SetMatrix(in matriz);
        canvas.DrawBitmap(
            origen,
            0,
            0,
            new SKSamplingOptions(SKFilterMode.Nearest),
            null);
        return salida;
    }

    private static SKMatrix MatrizOrientacion(SKEncodedOrigin orientacion, int ancho, int alto) =>
        orientacion switch
        {
            SKEncodedOrigin.TopRight => CrearMatriz(-1, 0, ancho, 0, 1, 0),
            SKEncodedOrigin.BottomRight => CrearMatriz(-1, 0, ancho, 0, -1, alto),
            SKEncodedOrigin.BottomLeft => CrearMatriz(1, 0, 0, 0, -1, alto),
            SKEncodedOrigin.LeftTop => CrearMatriz(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => CrearMatriz(0, -1, alto, 1, 0, 0),
            SKEncodedOrigin.RightBottom => CrearMatriz(0, -1, alto, -1, 0, ancho),
            SKEncodedOrigin.LeftBottom => CrearMatriz(0, 1, 0, -1, 0, ancho),
            _ => SKMatrix.Identity,
        };

    private static SKMatrix CrearMatriz(
        float escalaX,
        float sesgoX,
        float trasladoX,
        float sesgoY,
        float escalaY,
        float trasladoY) =>
        new(escalaX, sesgoX, trasladoX, sesgoY, escalaY, trasladoY, 0, 0, 1);
}
