using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador HTTP hacia ARGOS. No loguea imágenes, base64 ni embeddings.</summary>
public sealed partial class VerificadorIdentidadArgosHttp(
    HttpClient httpClient,
    IOptions<OpcionesArgos> opciones,
    ILogger<VerificadorIdentidadArgosHttp> logger)
    : IVerificadorIdentidadArgos
{
    public async Task<Result<VerificacionFacialResultado>> VerificarAsync(
        byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct)
    {
        var url = opciones.Value.Url.TrimEnd('/') + "/api/verify";
        var request = new VerificarRequest(
            Convert.ToBase64String(imagenDocumento),
            Convert.ToBase64String(imagenSelfie));

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(opciones.Value.ApiKey))
        {
            mensaje.Headers.TryAddWithoutValidation("X-Service-Key", opciones.Value.ApiKey);
        }

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await httpClient.SendAsync(mensaje, ct);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == ct)
        {
            RegistrarError(logger, "timeout");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.ServicioVerificacionNoDisponible,
                    "El servicio de verificación no está disponible. Intente más tarde."));
        }
        catch (HttpRequestException)
        {
            RegistrarError(logger, "red");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.ServicioVerificacionNoDisponible,
                    "El servicio de verificación no está disponible. Intente más tarde."));
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var imagenSinRostro = await ImagenSinRostroAsync(respuesta, ct);
            if (imagenSinRostro is not null)
            {
                RegistrarError(logger, $"rostro-no-detectado-{imagenSinRostro}");
                return Result.Fallo<VerificacionFacialResultado>(
                    new Error(ErroresKyc.RostroNoDetectado, MensajeRostroNoDetectado(imagenSinRostro)));
            }

            RegistrarError(logger, $"http-{(int)respuesta.StatusCode}");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.ServicioVerificacionNoDisponible,
                    "El servicio de verificación no está disponible. Intente más tarde."));
        }

        VerificarResponse? cuerpo;
        try
        {
            cuerpo = await respuesta.Content.ReadFromJsonAsync<VerificarResponse>(ct);
        }
        catch (Exception ex)
        {
            RegistrarError(logger, $"json-{ex.GetType().Name}");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.ServicioVerificacionNoDisponible,
                    "El servicio de verificación no está disponible. Intente más tarde."));
        }

        if (cuerpo is null)
        {
            RegistrarError(logger, "respuesta-vacia");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.ServicioVerificacionNoDisponible,
                    "El servicio de verificación no está disponible. Intente más tarde."));
        }

        if (!cuerpo.Success)
        {
            RegistrarError(logger, "success-false");
            return Result.Fallo<VerificacionFacialResultado>(
                new Error(ErroresKyc.VerificacionFacialFallida,
                    cuerpo.Message ?? "No se pudo verificar el rostro."));
        }

        if (!cuerpo.Verified)
        {
            return Result.Exito(new VerificacionFacialResultado(
                Coinciden: false,
                SimilitudPercent: cuerpo.SimilarityPercent,
                MotivoRechazo: cuerpo.Message ?? "El rostro no coincide con el documento."));
        }

        return Result.Exito(new VerificacionFacialResultado(
            Coinciden: true,
            SimilitudPercent: cuerpo.SimilarityPercent,
            MotivoRechazo: null));
    }

    private sealed record VerificarRequest(string Image1, string Image2);

    private sealed record VerificarResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("verified")] bool Verified,
        [property: JsonPropertyName("distance")] double? Distance,
        [property: JsonPropertyName("threshold")] double? Threshold,
        [property: JsonPropertyName("similarity_percent")] double SimilarityPercent,
        [property: JsonPropertyName("message")] string? Message);

    // Contrato de error de ARGOS: 422 con {"code":"face-not-detected","image":"imgN"} (versión
    // corregida) o 500 con {"error":"Exception while processing imgN_path..."} (versión actual).
    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("error")] string? Error);

    // Clasifica errores de entrada del usuario (foto sin rostro) para no tratarlos como caída
    // del servicio. Devuelve "documento" (img1), "selfie" (img2) o "desconocida"; null si no
    // es un caso de rostro no detectado.
    private static async Task<string?> ImagenSinRostroAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        ErrorResponse? cuerpo;
        try
        {
            cuerpo = await respuesta.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        }
        catch (Exception)
        {
            return null; // Cuerpo ilegible: se trata como fallo del servicio.
        }

        if (cuerpo is null)
        {
            return null;
        }

        if ((int)respuesta.StatusCode == 422 && cuerpo.Code == "face-not-detected")
        {
            return NombreImagen(cuerpo.Image);
        }

        const string patron = "Exception while processing img";
        var error = cuerpo.Error;
        if (error is not null && error.Contains(patron, StringComparison.Ordinal))
        {
            var indice = error.IndexOf(patron, StringComparison.Ordinal) + patron.Length;
            return indice < error.Length ? NombreImagen($"img{error[indice]}") : "desconocida";
        }

        return null;
    }

    private static string NombreImagen(string? imagen) => imagen switch
    {
        "img1" => "documento",
        "img2" => "selfie",
        _ => "desconocida",
    };

    private static string MensajeRostroNoDetectado(string imagen) =>
        imagen switch
        {
            "documento" => "No se detectó un rostro en la foto del documento. Usa una foto frontal, nítida y con buena luz, e inténtalo de nuevo.",
            "selfie" => "No se detectó un rostro en la selfie. Usa una foto frontal, nítida y con buena luz, e inténtalo de nuevo.",
            _ => "No se detectó un rostro en una de las fotos. Usa fotos frontales, nítidas y con buena luz, e inténtalo de nuevo.",
        };

    // Auditoría sin PII: solo tipo de error de comunicación. Sin URLs completas, base64 ni bytes.
    [LoggerMessage(Level = LogLevel.Warning, Message = "ARGOS no disponible: tipo={TipoError}")]
    private static partial void RegistrarError(ILogger logger, string tipoError);
}
