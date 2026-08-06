using System.Net;
using System.Text;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class VerificadorIdentidadArgosHttpTests
{
    private static readonly byte[] _imagen = [0x89, 0x50, 0x4E, 0x47];

    private static VerificadorIdentidadArgosHttp Crear(
        HttpMessageHandler handler, string url = "http://argos.test", string? apiKey = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
        var opciones = Options.Create(new OpcionesArgos { Url = url, ApiKey = apiKey });
        return new VerificadorIdentidadArgosHttp(client, opciones, NullLogger<VerificadorIdentidadArgosHttp>.Instance);
    }

    [Fact]
    public async Task Respuesta_verified_true_devuelve_coinciden_con_score()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success": true, "verified": true, "similarity_percent": 87.5}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.True(r.Valor.Coinciden);
        Assert.Equal(87.5, r.Valor.SimilitudPercent);
    }

    [Fact]
    public async Task Respuesta_verified_false_devuelve_no_coinciden_con_motivo()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success": true, "verified": false, "similarity_percent": 32.0, "message": "No coincide"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.False(r.Valor.Coinciden);
        Assert.Equal("No coincide", r.Valor.MotivoRechazo);
    }

    [Fact]
    public async Task Error_de_red_devuelve_servicio_no_disponible()
    {
        var handler = new FakeHandler(new HttpRequestException("No route"));
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.ServicioVerificacionNoDisponible, r.Error.Code);
    }

    [Fact]
    public async Task Http_500_devuelve_servicio_no_disponible()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.ServicioVerificacionNoDisponible, r.Error.Code);
    }

    [Fact]
    public async Task Json_invalido_devuelve_servicio_no_disponible()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success": true""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.ServicioVerificacionNoDisponible, r.Error.Code);
    }

    [Fact]
    public async Task Success_false_devuelve_VerificacionFacialFallida()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success": false, "message": "Rostro no detectado"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.VerificacionFacialFallida, r.Error.Code);
        Assert.Equal("Rostro no detectado", r.Error.Message);
    }

    [Fact]
    public async Task Http_422_face_not_detected_en_img1_devuelve_rostro_no_detectado_en_documento()
    {
        var handler = new FakeHandler(new HttpResponseMessage((HttpStatusCode)422)
        {
            Content = new StringContent(
                """{"success": false, "code": "face-not-detected", "image": "img1", "error": "No se detectó rostro"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.RostroNoDetectado, r.Error.Code);
        Assert.Contains("documento", r.Error.Message);
    }

    [Fact]
    public async Task Http_422_face_not_detected_en_img2_devuelve_rostro_no_detectado_en_selfie()
    {
        var handler = new FakeHandler(new HttpResponseMessage((HttpStatusCode)422)
        {
            Content = new StringContent(
                """{"success": false, "code": "face-not-detected", "image": "img2", "error": "No se detectó rostro"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.RostroNoDetectado, r.Error.Code);
        Assert.Contains("selfie", r.Error.Message);
    }

    [Fact]
    public async Task Http_500_con_payload_de_imagen_sin_rostro_devuelve_rostro_no_detectado()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                """{"success": false, "error": "Exception while processing img2_path: Face could not be detected"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.RostroNoDetectado, r.Error.Code);
        Assert.Contains("selfie", r.Error.Message);
    }

    [Fact]
    public async Task Http_500_sin_payload_conocido_sigue_siendo_servicio_no_disponible()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                """{"success": false, "error": "Invalid base64-encoded string"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var verificador = Crear(handler);

        var r = await verificador.VerificarAsync(_imagen, _imagen, CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.ServicioVerificacionNoDisponible, r.Error.Code);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _respuesta;
        private readonly Exception? _excepcion;

        public FakeHandler(HttpResponseMessage respuesta) => _respuesta = respuesta;

        public FakeHandler(Exception excepcion) => _excepcion = excepcion;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_excepcion is not null)
            {
                return Task.FromException<HttpResponseMessage>(_excepcion);
            }

            return Task.FromResult(_respuesta!);
        }
    }
}
