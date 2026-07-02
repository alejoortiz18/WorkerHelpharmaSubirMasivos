using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Models.Dto;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public class SoporteApiService
{
    private static readonly JsonSerializerOptions SoporteJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly IIntegracionConfigProvider _config;
    private readonly ILogger<SoporteApiService> _logger;

    public SoporteApiService(
        HttpClient httpClient,
        IIntegracionConfigProvider config,
        ILogger<SoporteApiService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<SoporteResponseDto?> EnviarSoporteAsync(string soporte)
    {
        try
        {
            var soporteConsulta = soporte.Trim();
            var json = JsonSerializer.Serialize(new { soporte = soporteConsulta });
            var endpoint = await _config.ObtenerSoporteApiUrlAsync();
            var apiKey = await _config.ObtenerSoporteApiKeyAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            request.Headers.Add("X-API-KEY", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ApiSoporteError | Soporte={Soporte} | Endpoint={Endpoint} | Body={Body} | Status={Status} | Respuesta={Respuesta}",
                    soporteConsulta,
                    endpoint,
                    json,
                    response.StatusCode,
                    contenido);
                return null;
            }

            if (string.IsNullOrWhiteSpace(contenido))
            {
                _logger.LogError(
                    "ApiSoporteError | Soporte={Soporte} | Status={Status} | Respuesta=vacia",
                    soporteConsulta,
                    response.StatusCode);
                return null;
            }

            return JsonSerializer.Deserialize<SoporteResponseDto>(contenido, SoporteJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiSoporteException | Soporte={Soporte}", soporte);
            return null;
        }
    }

}
