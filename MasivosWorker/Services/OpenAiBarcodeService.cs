using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IronPdf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Services;

public class OpenAiBarcodeService : IOpenAiBarcodeService
{
    private static readonly Regex CodigoValido =
        new(@"^([A-Z]+)(\d+)$", RegexOptions.Compiled);

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

        byte[] imagenBytes;
        try
        {
            imagenBytes = RenderizarPrimeraPagina(rutaPdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAiRenderFallo | Ruta={Ruta}", rutaPdf);
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = ex.Message
            };
        }

        var base64 = Convert.ToBase64String(imagenBytes);
        Exception? ultimoError = null;

        for (int intento = 1; intento <= Math.Max(1, _settings.MaxReintentos); intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var respuestaTexto = await EnviarSolicitudAsync(base64, cancellationToken);
                var resultado = InterpretarRespuesta(respuestaTexto);

                _logger.LogInformation(
                    "OpenAiResultado | Archivo={Archivo} | Tipo={Tipo} | Codigo={Codigo}",
                    Path.GetFileName(rutaPdf),
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
                    Path.GetFileName(rutaPdf),
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

    private OpenAiBarcodeResult InterpretarRespuesta(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();

        if (string.Equals(limpio, "NO_BARCODE", StringComparison.OrdinalIgnoreCase))
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.NoBarcode
            };
        }

        var codigo = limpio.Replace(" ", "").Replace("-", "");
        var match = CodigoValido.Match(codigo);

        if (!match.Success)
        {
            return new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.NoBarcode
            };
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

    private async Task<string?> EnviarSolicitudAsync(string base64Png, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

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
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/png;base64,{base64Png}"
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

    private static byte[] RenderizarPrimeraPagina(string rutaPdf)
    {
        using var pdf = PdfDocument.FromFile(rutaPdf);
        using var primeraPagina = pdf.CopyPage(0);
        var imagenes = primeraPagina.ToBitmap(400).ToList();
        using var bitmap = (Bitmap)imagenes[0];

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
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
