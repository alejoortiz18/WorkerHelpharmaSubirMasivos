using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Services;

public class RadicaWebApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly RadicaWebSettings _settings;
    private readonly ILogger<RadicaWebApiService> _logger;

    public RadicaWebApiService(
        HttpClient httpClient,
        IOptions<RadicaWebSettings> settings,
        ILogger<RadicaWebApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RadicaWebBusquedaResultado> EnviarBusquedaAsync(
        DateOnly fecha,
        string bodega,
        CancellationToken cancellationToken = default)
    {
        var fechaTexto = fecha.ToString("yyyy-MM-dd");
        var bodegaTexto = bodega.Trim();

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                fecha = fechaTexto,
                bodega = bodegaTexto
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
            request.Headers.Add("x-api-client", _settings.ApiClient);
            request.Headers.Add("x-api-secret", _settings.ApiSecret);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var contenido = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(contenido))
            {
                var exito = JsonSerializer.Deserialize<RadicaWebBusquedaExitoDto>(contenido, JsonOptions);
                _logger.LogInformation(
                    "RadicaWebBusquedaOK | Fecha={Fecha} | Bodega={Bodega} | SolicitudId={SolicitudId}",
                    fechaTexto,
                    bodegaTexto,
                    exito?.SolicitudId);

                return new RadicaWebBusquedaResultado
                {
                    HttpStatusCode = (int)response.StatusCode,
                    Success = exito?.Success,
                    Message = exito?.Message,
                    SolicitudId = exito?.SolicitudId,
                    RegistrosInsertados = exito?.RegistrosInsertados,
                    TotalRegistros = exito?.TotalRegistros,
                    JobId = exito?.JobId,
                    Path = _settings.ApiUrl
                };
            }

            var error = string.IsNullOrWhiteSpace(contenido)
                ? null
                : JsonSerializer.Deserialize<RadicaWebBusquedaErrorDto>(contenido, JsonOptions);

            _logger.LogWarning(
                "RadicaWebBusquedaError | Fecha={Fecha} | Bodega={Bodega} | Status={Status} | Message={Message}",
                fechaTexto,
                bodegaTexto,
                (int)response.StatusCode,
                error?.Message ?? contenido);

            return new RadicaWebBusquedaResultado
            {
                HttpStatusCode = error?.StatusCode ?? (int)response.StatusCode,
                Success = false,
                Message = error?.Message ?? contenido,
                Error = error?.Error,
                Timestamp = ParseTimestamp(error?.Timestamp),
                Path = error?.Path ?? _settings.ApiUrl
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "RadicaWebBusquedaException | Fecha={Fecha} | Bodega={Bodega}",
                fechaTexto,
                bodegaTexto);

            return new RadicaWebBusquedaResultado
            {
                Success = false,
                Message = ex.Message,
                Error = ex.GetType().Name,
                Path = _settings.ApiUrl
            };
        }
    }

    private static DateTimeOffset? ParseTimestamp(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return DateTimeOffset.TryParse(valor, out var parsed) ? parsed : null;
    }

    private sealed class RadicaWebBusquedaExitoDto
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public int? SolicitudId { get; set; }

        public int? RegistrosInsertados { get; set; }

        public int? TotalRegistros { get; set; }

        public string? JobId { get; set; }
    }

    private sealed class RadicaWebBusquedaErrorDto
    {
        public int StatusCode { get; set; }

        public string? Message { get; set; }

        public string? Error { get; set; }

        public string? Timestamp { get; set; }

        public string? Path { get; set; }
    }
}
