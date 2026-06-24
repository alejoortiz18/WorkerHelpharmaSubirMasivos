using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Services;

public class OpenAiBarcodeService : IOpenAiBarcodeService
{
    internal const string NombreArchivoNeutroOpenAi = "documento.pdf";

    private static readonly Regex CodigoValido =
        new(@"^([A-Z]+)-?(\d+)$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiBarcodeService> _logger;
    private readonly string? _prompt;

    public OpenAiBarcodeService(
        HttpClient httpClient,
        IOptions<OpenAiSettings> settings,
        ILogger<OpenAiBarcodeService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _prompt = CargarPrompt(_settings.PromptResourcePath);
    }

    public async Task<OpenAiBarcodeResult> LeerCodigoAsync(
        string rutaPdf,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = "ApiKey de OpenAI no configurada."
            };
        }

        if (string.IsNullOrWhiteSpace(_prompt))
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = "Prompt de OpenAI no encontrado."
            };
        }

        if (!File.Exists(rutaPdf))
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = $"PDF no encontrado: {rutaPdf}"
            };
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = await File.ReadAllBytesAsync(rutaPdf, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAiLecturaPdfFallo | Ruta={Ruta}", rutaPdf);
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = ex.Message
            };
        }

        var nombreArchivo = Path.GetFileName(rutaPdf);
        Exception? ultimoError = null;

        for (int intento = 1; intento <= Math.Max(1, _settings.MaxReintentos); intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var respuestaTexto = await EnviarSolicitudAsync(pdfBytes, cancellationToken);
                var resultado = InterpretarRespuesta(respuestaTexto);

                _logger.LogInformation(
                    "OpenAiRespuestaCruda | Archivo={Archivo} | Texto={Texto}",
                    nombreArchivo,
                    respuestaTexto ?? "-");

                _logger.LogInformation(
                    "OpenAiResultado | Archivo={Archivo} | Modelo={Modelo} | Tipo={Tipo} | Codigo={Codigo}",
                    nombreArchivo,
                    _settings.Model,
                    resultado.Tipo,
                    resultado.Codigo ?? "-");

                return resultado;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ultimoError = ex;
                _logger.LogWarning(
                    ex,
                    "OpenAiFallo | Archivo={Archivo} | Reintento={Reintento}",
                    nombreArchivo,
                    intento);
            }

            if (intento < _settings.MaxReintentos)
                await Task.Delay(500 * intento, cancellationToken);
        }

        return new OpenAiBarcodeResult
        {
            Tipo = OpenAiBarcodeResultKind.ErrorServicio,
            ErrorMensaje = ultimoError?.Message ?? "OpenAI no respondió tras los reintentos configurados."
        };
    }

    public static OpenAiBarcodeResult InterpretarRespuesta(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();

        if (string.Equals(limpio, "NO_BARCODE", StringComparison.OrdinalIgnoreCase))
        {
            return new OpenAiBarcodeResult { Tipo = OpenAiBarcodeResultKind.NoBarcode };
        }

        var codigo = limpio
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("*", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        var match = CodigoValido.Match(codigo);
        if (!match.Success)
        {
            return new OpenAiBarcodeResult { Tipo = OpenAiBarcodeResultKind.NoBarcode };
        }

        var documento = new DocumentoProcesadoDto
        {
            Prefijo = match.Groups[1].Value,
            Numero = match.Groups[2].Value,
            NombreArchivo = $"{codigo}.pdf"
        };

        return new OpenAiBarcodeResult
        {
            Tipo = OpenAiBarcodeResultKind.CodigoEncontrado,
            Codigo = codigo,
            Documento = documento
        };
    }

    private async Task<string?> EnviarSolicitudAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var pdfBase64 = Convert.ToBase64String(pdfBytes);
        var body = new
        {
            model = _settings.Model,
            temperature = 0,
            max_tokens = 32,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = _prompt },
                        new
                        {
                            type = "file",
                            file = new
                            {
                                filename = NombreArchivoNeutroOpenAi,
                                file_data = $"data:application/pdf;base64,{pdfBase64}"
                            }
                        }
                    }
                }
            }
        };

        request.Content = JsonContent.Create(body);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _settings.TimeoutSeconds)));

        using var response = await _httpClient.SendAsync(request, cts.Token);
        var json = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI HTTP {(int)response.StatusCode}: {json}");

        var parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(json);
        return parsed?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private static string? CargarPrompt(string promptResourcePath)
    {
        var ruta = Path.IsPathRooted(promptResourcePath)
            ? promptResourcePath
            : Path.Combine(AppContext.BaseDirectory, promptResourcePath);

        return File.Exists(ruta) ? File.ReadAllText(ruta) : null;
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
