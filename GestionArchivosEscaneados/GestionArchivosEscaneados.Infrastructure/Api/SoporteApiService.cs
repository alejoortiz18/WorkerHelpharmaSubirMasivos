using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public class SoporteApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SoporteApiService> _logger;
    private readonly string _apiKey;

    public SoporteApiService(
        HttpClient httpClient,
        ILogger<SoporteApiService> logger,
        IOptions<ApiCredentialsSettings> credenciales)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = credenciales.Value.SoporteApiKey;
    }

    public async Task<SoporteResponseDto?> EnviarSoporteAsync(string soporte)
    {
        try
        {
            var soporteNormalizado = NormalizarSoporte(soporte);
            var json = JsonSerializer.Serialize(new { soporte = soporteNormalizado });
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api-soportes.helpharma.com.co/api/DocSoporte/soportes/DatosSoportes");

            request.Headers.Add("X-API-KEY", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ApiSoporteError | Soporte={Soporte} | Status={Status}",
                    soporteNormalizado,
                    response.StatusCode);
                return null;
            }

            return JsonSerializer.Deserialize<SoporteResponseDto>(contenido,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiSoporteException | Soporte={Soporte}", soporte);
            return null;
        }
    }

    private static string NormalizarSoporte(string soporte) =>
        soporte.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
}
