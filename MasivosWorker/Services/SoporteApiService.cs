using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Services;

public class SoporteApiService
{
    private static readonly JsonSerializerOptions SoporteJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<SoporteApiService> _logger;
    private readonly string _apiKey;

    public SoporteApiService(HttpClient httpClient, ILogger<SoporteApiService> logger, IOptions<ApiCredentialsSettings> credenciales)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = credenciales.Value.SoporteApiKey;
    }

    public async Task<SoporteResponseDto?> EnviarSoporteAsync(string soporte)
    {
        try
        {
            var soporteConsulta = soporte.Trim();
            var body = new
            {
                soporte = soporteConsulta
            };

            var json = JsonSerializer.Serialize(body);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api-soportes.helpharma.com.co/api/DocSoporte/soportes/DatosSoportes");

            request.Headers.Add("X-API-KEY", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            var contenido = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(contenido))
                {
                    _logger.LogError(
                        "ApiSoporteError | Soporte={Soporte} | Status={Status} | Respuesta=vacia",
                        soporteConsulta,
                        response.StatusCode);
                    return null;
                }

                var resultado = JsonSerializer.Deserialize<SoporteResponseDto>(contenido, SoporteJsonOptions);

                _logger.LogInformation(
                    "ApiSoporteOK | Soporte={Soporte} | Paciente={Paciente}",
                    soporteConsulta,
                    resultado?.NombrePaciente
                );

                return resultado;
            }
            else
            {
                _logger.LogError(
                    "ApiSoporteError | Soporte={Soporte} | Status={Status} | Respuesta={Respuesta}",
                    soporteConsulta,
                    response.StatusCode,
                    contenido
                );

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ApiSoporteException | Soporte={Soporte}",
                soporte
            );

            return null;
        }
    }

}
