using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Dto;
using Services;
using Xunit;

namespace Tests.Services;

public class RadicaWebApiServiceTests
{
    [Fact]
    public async Task EnviarBusquedaAsync_Exito_DeserializaRespuesta()
    {
        var handler = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                    "success": true,
                    "message": "Búsqueda de soporte físico encolada exitosamente",
                    "solicitudId": 44,
                    "registrosInsertados": 8,
                    "totalRegistros": 8,
                    "jobId": "ms-search-physical-supports-busqueda-44-1783081179470"
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var service = CrearServicio(httpClient);

        var resultado = await service.EnviarBusquedaAsync(
            new DateOnly(2025, 7, 2),
            "FARMACIAMEDELLIN");

        resultado.Success.Should().BeTrue();
        resultado.SolicitudId.Should().Be(44);
        resultado.Message.Should().Contain("encolada");
        handler.LastBody.Should().Contain("\"fecha\":\"2025-07-02\"");
        handler.LastBody.Should().Contain("\"bodega\":\"FARMACIAMEDELLIN\"");
    }

    [Fact]
    public async Task EnviarBusquedaAsync_Error400_RegistraCamposDeError()
    {
        var handler = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                    "statusCode": 400,
                    "message": "Ya se ejecutó hoy una búsqueda",
                    "error": "Bad Request",
                    "timestamp": "2026-07-03T12:20:49.281Z",
                    "path": "/api/physical-supports/integrations/busqueda"
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var service = CrearServicio(httpClient);

        var resultado = await service.EnviarBusquedaAsync(
            new DateOnly(2026, 7, 2),
            "FARMACIAMEDELLIN");

        resultado.Success.Should().BeFalse();
        resultado.HttpStatusCode.Should().Be(400);
        resultado.Error.Should().Be("Bad Request");
        resultado.Timestamp.Should().NotBeNull();
    }

    private static RadicaWebApiService CrearServicio(HttpClient httpClient) =>
        new(
            httpClient,
            Options.Create(new RadicaWebSettings
            {
                ApiUrl = "https://api-radicacion.test/busqueda",
                ApiClient = "client-test",
                ApiSecret = "secret-test"
            }),
            NullLogger<RadicaWebApiService>.Instance);

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }
}
