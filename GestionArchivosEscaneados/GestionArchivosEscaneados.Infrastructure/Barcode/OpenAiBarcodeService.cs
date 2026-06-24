using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Barcode;

public class OpenAiBarcodeService : IOpenAiBarcodeService
{
    /// <summary>Nombre neutro enviado a OpenAI para evitar filtrar pistas desde el nombre del archivo UNC.</summary>
    internal const string NombreArchivoNeutroOpenAi = "documento.pdf";

    private static readonly Regex CodigoValido =
        new(@"^([A-Z]+)-?(\d+)$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiBarcodeService> _logger;
    private readonly IConfiguracionesService _configuraciones;
    private string? _prompt;
    private bool _promptCargado;

    public OpenAiBarcodeService(
        HttpClient httpClient,
        IOptions<OpenAiSettings> settings,
        IConfiguracionesService configuraciones,
        ILogger<OpenAiBarcodeService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _configuraciones = configuraciones;
        _logger = logger;
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

        var prompt = await CargarPromptAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = "Prompt de OpenAI no encontrado en BD ni en archivo."
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

        for (var intento = 1; intento <= Math.Max(1, _settings.MaxReintentos); intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var respuestaTexto = await EnviarSolicitudAsync(pdfBytes, prompt, cancellationToken);
                var resultado = InterpretarRespuesta(respuestaTexto);
                resultado.RespuestaCruda = respuestaTexto?.Trim();

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
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.NoBarcode,
                RespuestaCruda = limpio
            };
        }

        var codigo = limpio
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        var match = CodigoValido.Match(codigo);
        if (!match.Success)
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.NoBarcode,
                RespuestaCruda = limpio
            };
        }

        return new OpenAiBarcodeResult
        {
            Tipo = OpenAiBarcodeResultKind.CodigoEncontrado,
            Codigo = codigo,
            RespuestaCruda = limpio
        };
    }

    private async Task<string?> EnviarSolicitudAsync(
        byte[] pdfBytes,
        string prompt,
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
                        new { type = "text", text = prompt },
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

    private Task<string> CargarPromptAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_promptCargado && _prompt != null)
            return Task.FromResult(_prompt);

        var promptDeArchivo = CargarPromptDeArchivo(_settings.PromptResourcePath);
        _prompt = promptDeArchivo;
        _promptCargado = true;

        _logger.LogInformation(
            "OpenAiPromptCargadoDeArchivo | Caracteres={Caracteres} | Inicio={Inicio}",
            _prompt.Length,
            _prompt.Split('\n')[0].Trim());

        SincronizarPromptEnBd(_prompt);
        return Task.FromResult(_prompt);
    }

    private void SincronizarPromptEnBd(string promptDeArchivo)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _configuraciones.GuardarAsync(
                    "OpenAi:PromptBarcode",
                    promptDeArchivo,
                    "Prompt para detección de códigos de barras en OpenAI",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NoSeGuardoPromptEnBD");
            }
        }, CancellationToken.None);
    }

    private static string CargarPromptDeArchivo(string promptResourcePath)
    {
        var ruta = Path.IsPathRooted(promptResourcePath)
            ? promptResourcePath
            : Path.Combine(AppContext.BaseDirectory, promptResourcePath);

        if (!File.Exists(ruta))
            throw new FileNotFoundException(
                $"Prompt OpenAI no encontrado. Debe existir Prompts/barcode-openai.txt en: {ruta}");

        return File.ReadAllText(ruta, System.Text.Encoding.UTF8).TrimEnd();
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
