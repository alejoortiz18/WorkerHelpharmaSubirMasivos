using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Dto;
using Services;
using Xunit;

namespace Tests.Services;

public class SoporteNormalizationTests
{
    [Fact]
    public async Task EnviarSoporteAsync_ConservaGuionesAntesDeEnviar()
    {
        var handler = new CaptureHandler(async _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"NombrePaciente\":\"Prueba\"}", Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(handler);
        var service = new SoporteApiService(
            httpClient,
            NullLogger<SoporteApiService>.Instance,
            Options.Create(new ApiCredentialsSettings { SoporteApiKey = "key" }));

        await service.EnviarSoporteAsync("D1-1523229");

        handler.LastRequest.Should().NotBeNull();
        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("\"soporte\":\"D1-1523229\"");
    }

    [Fact]
    public async Task EnviarSoporteFisicoAsync_QuitaGuionesAntesDeEnviar()
    {
        var handler = new CaptureHandler(async _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(handler);
        var service = new SoporteFisicoApiService(
            httpClient,
            NullLogger<SoporteFisicoApiService>.Instance,
            Options.Create(new ApiCredentialsSettings
            {
                SoporteFisicoToken = "token",
                IdUsuario = "system"
            }));

        var pdfPath = Path.Combine(Path.GetTempPath(), $"soporte-normalizacion-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, [0x25, 0x50, 0x44, 0x46]);

        try
        {
            await service.EnviarSoporteFisicoAsync(
                "D1-1523229",
                pdfPath,
                new SoporteResponseDto
                {
                    IdConvenio = "01",
                    NombreConvenio = "EPS",
                    Fecha = new DateTime(2026, 5, 28),
                    IdBodega = "B1",
                    NombreSede = "S1",
                    NombreActividad = "A1",
                    TipoEntrega = "T1",
                    TipoPlan = "P1",
                    IdCartera = "C1",
                    NombrePaciente = "Paciente Prueba",
                    IdTipoId = "CC",
                    IdPaciente = "123",
                    Celular = "3000000000",
                    Telefono = "3000000001",
                    Direccion = "Direccion",
                    Complemento = "Complemento",
                    Observacion = "Obs",
                    ValorCM = "0",
                    medicamentos = []
                });

            handler.LastRequest.Should().NotBeNull();
            var body = handler.LastBody;
            body.Should().NotBeNull();
            body.Should().Contain("name=soporte");
            body.Should().Contain("D11523229");
            body.Should().NotContain("D1-1523229");
            body.Should().NotContain("D11523229-");
            body.Should().Contain("name=idConvenio");
            body.Should().Contain("1");
            body.Should().NotContain("name=idConvenio\r\n\r\n01\r\n");
        }
        finally
        {
            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
        }
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return await responder(request);
        }

    }
}
