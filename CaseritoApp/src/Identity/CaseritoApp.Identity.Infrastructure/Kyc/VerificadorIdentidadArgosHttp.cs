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

    // Auditoría sin PII: solo tipo de error de comunicación. Sin URLs completas, base64 ni bytes.
    [LoggerMessage(Level = LogLevel.Warning, Message = "ARGOS no disponible: tipo={TipoError}")]
    private static partial void RegistrarError(ILogger logger, string tipoError);
}
