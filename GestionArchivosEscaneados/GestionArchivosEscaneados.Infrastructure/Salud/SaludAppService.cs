using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Entities;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Salud;

public class SaludAppService
{
    private readonly IIntegracionConfigProvider _config;
    private readonly IConfiguracionProductoService _productos;
    private readonly UncConexionService _unc;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SaludAppService> _logger;

    public SaludAppService(
        IIntegracionConfigProvider config,
        IConfiguracionProductoService productos,
        UncConexionService unc,
        IHttpClientFactory httpClientFactory,
        ILogger<SaludAppService> logger)
    {
        _config = config;
        _productos = productos;
        _unc = unc;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SaludPanel> ObtenerPanelAsync(CancellationToken cancellationToken = default)
    {
        await _productos.SembrarDesdeAppSettingsSiFaltanAsync(cancellationToken);

        var integraciones = new List<IntegracionSaludItem>
        {
            await ConstruirAsync(ProductoIntegracion.Unc, cancellationToken),
            await ConstruirAsync(ProductoIntegracion.SoporteApi, cancellationToken),
            await ConstruirAsync(ProductoIntegracion.SoporteFisico, cancellationToken),
            await ConstruirAsync(ProductoIntegracion.OpenAi, cancellationToken)
        };

        return new SaludPanel { Integraciones = integraciones };
    }

    public Task<SaludPanel> VerificarAsync(CancellationToken cancellationToken = default) =>
        ObtenerPanelAsync(cancellationToken);

    public async Task GuardarIntegracionAsync(
        SaludIntegracionGuardarRequest request,
        CancellationToken cancellationToken = default)
    {
        var producto = ProductoIntegracion.ProductoDesdeId(request.Id)
            ?? throw new ArgumentException($"Integración desconocida: {request.Id}");

        var actual = await _productos.ObtenerAsync(producto, cancellationToken)
            ?? new ConfiguracionProducto { Producto = producto };

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
            actual.Endpoint = request.Endpoint.Trim();

        if (!string.IsNullOrWhiteSpace(request.EndpointVerificacion))
            actual.EndpointVerificacion = request.EndpointVerificacion.Trim();

        if (!SecretoUi.DebeConservarValorExistente(request.ClaveCredencial, !string.IsNullOrWhiteSpace(actual.ClaveCredencial))
            && !string.IsNullOrWhiteSpace(request.ClaveCredencial))
            actual.ClaveCredencial = request.ClaveCredencial.Trim();

        if (request.ValorAdicional is not null)
        {
            if (EsCampoSecreto(producto, secundario: true))
            {
                if (!SecretoUi.DebeConservarValorExistente(request.ValorAdicional, !string.IsNullOrWhiteSpace(actual.ValorAdicional))
                    && !string.IsNullOrWhiteSpace(request.ValorAdicional))
                    actual.ValorAdicional = request.ValorAdicional.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(request.ValorAdicional))
            {
                actual.ValorAdicional = request.ValorAdicional.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
            actual.Prompt = request.Prompt.Trim();

        if (!string.IsNullOrWhiteSpace(request.Descripcion))
            actual.Descripcion = request.Descripcion.Trim();

        await _productos.GuardarAsync(actual, cancellationToken);

        if (producto == ProductoIntegracion.Unc)
            _unc.InvalidarConexionRed();
    }

    private async Task<IntegracionSaludItem> ConstruirAsync(string producto, CancellationToken cancellationToken)
    {
        var registro = await _productos.ObtenerAsync(producto, cancellationToken);
        var item = new IntegracionSaludItem
        {
            Id = ProductoIntegracion.IdDesdeProducto(producto),
            Producto = producto,
            Endpoint = registro?.Endpoint ?? string.Empty,
            EndpointVerificacion = registro?.EndpointVerificacion,
            ClaveCredencial = registro?.ClaveCredencial ?? string.Empty,
            ValorAdicional = registro?.ValorAdicional,
            Prompt = registro?.Prompt,
            Descripcion = registro?.Descripcion,
            UsaPrompt = producto == ProductoIntegracion.OpenAi,
            UsaEndpointVerificacion = producto == ProductoIntegracion.OpenAi,
            GuardadoEnBd = registro is not null
        };

        await VerificarIntegracionAsync(item, registro, cancellationToken);
        PrepararCredencialesVista(item);
        return item;
    }

    private async Task VerificarIntegracionAsync(
        IntegracionSaludItem item,
        ConfiguracionProducto? registro,
        CancellationToken cancellationToken)
    {
        switch (item.Producto)
        {
            case var p when p == ProductoIntegracion.Unc:
                var accesible = _unc.AsegurarAccesoUnc();
                item.Activo = accesible;
                if (!accesible)
                    item.Error = _unc.UltimoErrorMensaje ?? "No se puede acceder a la ruta UNC.";
                break;

            case var p when p == ProductoIntegracion.SoporteApi:
                await VerificarHttpPostJsonAsync(
                    item,
                    registro?.Endpoint ?? await _config.ObtenerSoporteApiUrlAsync(cancellationToken),
                    registro?.ClaveCredencial ?? await _config.ObtenerSoporteApiKeyAsync(cancellationToken),
                    "X-API-KEY",
                    "{\"soporte\":\"SALUD_CHECK\"}",
                    cancellationToken);
                break;

            case var p when p == ProductoIntegracion.SoporteFisico:
                await VerificarHttpGetAsync(
                    item,
                    registro?.Endpoint ?? await _config.ObtenerSoporteFisicoApiUrlAsync(cancellationToken),
                    registro?.ClaveCredencial ?? await _config.ObtenerSoporteFisicoTokenAsync(cancellationToken),
                    cancellationToken);
                break;

            case var p when p == ProductoIntegracion.OpenAi:
                var modelsUrl = registro?.EndpointVerificacion ?? await _config.ObtenerOpenAiModelsUrlAsync(cancellationToken);
                await VerificarHttpGetAsync(
                    item,
                    modelsUrl,
                    registro?.ClaveCredencial ?? await _config.ObtenerOpenAiApiKeyAsync(cancellationToken),
                    cancellationToken,
                    bearer: true);
                break;
        }
    }

    private static bool EsCampoSecreto(string producto, bool secundario) =>
        producto switch
        {
            ProductoIntegracion.Unc => secundario,
            ProductoIntegracion.SoporteApi => !secundario,
            ProductoIntegracion.SoporteFisico => !secundario,
            ProductoIntegracion.OpenAi => !secundario,
            _ => false
        };

    private static void PrepararCredencialesVista(IntegracionSaludItem item)
    {
        item.TieneClaveCredencial = !string.IsNullOrWhiteSpace(item.ClaveCredencial);
        if (EsCampoSecreto(item.Producto, secundario: false) && item.TieneClaveCredencial)
        {
            item.ClaveCredencialEnmascarada = SecretoUi.Enmascarar(item.ClaveCredencial);
            item.ClaveCredencial = string.Empty;
            item.OcultarClaveCredencial = true;
        }
        else
        {
            item.ClaveCredencialEnmascarada = item.ClaveCredencial;
        }

        item.TieneValorAdicional = !string.IsNullOrWhiteSpace(item.ValorAdicional);
        if (EsCampoSecreto(item.Producto, secundario: true) && item.TieneValorAdicional)
        {
            item.ValorAdicionalEnmascarado = SecretoUi.Enmascarar(item.ValorAdicional);
            item.ValorAdicional = string.Empty;
            item.OcultarValorAdicional = true;
        }
        else
        {
            item.ValorAdicionalEnmascarado = item.ValorAdicional ?? string.Empty;
        }
    }

    private async Task VerificarHttpPostJsonAsync(
        IntegracionSaludItem item,
        string endpoint,
        string apiKey,
        string headerName,
        string jsonBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            item.Activo = false;
            item.Error = "Endpoint no configurado.";
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            item.Activo = false;
            item.Error = "Clave API no configurada.";
            return;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(SaludAppService));
            client.Timeout = TimeSpan.FromSeconds(30);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation(headerName, apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                item.Activo = false;
                item.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            item.Activo = true;
            if (!response.IsSuccessStatusCode)
                item.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} (conexión disponible)";
        }
        catch (Exception ex)
        {
            item.Activo = false;
            item.Error = ex.Message;
            _logger.LogWarning(ex, "SaludVerificacionFallo | Producto={Producto}", item.Producto);
        }
    }

    private async Task VerificarHttpGetAsync(
        IntegracionSaludItem item,
        string endpoint,
        string credential,
        CancellationToken cancellationToken,
        bool bearer = false)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            item.Activo = false;
            item.Error = "Endpoint no configurado.";
            return;
        }

        if (string.IsNullOrWhiteSpace(credential))
        {
            item.Activo = false;
            item.Error = "Credencial no configurada.";
            return;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(SaludAppService));
            client.Timeout = TimeSpan.FromSeconds(30);
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (bearer)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            else
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {credential}");

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                item.Activo = false;
                item.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            item.Activo = true;
            if (!response.IsSuccessStatusCode)
                item.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} (conexión disponible)";
        }
        catch (Exception ex)
        {
            item.Activo = false;
            item.Error = ex.Message;
            _logger.LogWarning(ex, "SaludVerificacionFallo | Producto={Producto}", item.Producto);
        }
    }
}

public record SaludPanel
{
    public IReadOnlyList<IntegracionSaludItem> Integraciones { get; init; } = [];

    public bool RequiereReinicio { get; init; }
}

public class IntegracionSaludItem
{
    public string Id { get; init; } = string.Empty;

    public string Producto { get; init; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string? EndpointVerificacion { get; set; }

    public string ClaveCredencial { get; set; } = string.Empty;

    public string? ValorAdicional { get; set; }

    public string? Prompt { get; set; }

    public string? Descripcion { get; set; }

    public bool UsaPrompt { get; init; }

    public bool UsaEndpointVerificacion { get; init; }

    public string ClaveCredencialEnmascarada { get; set; } = string.Empty;

    public string ValorAdicionalEnmascarado { get; set; } = string.Empty;

    public bool TieneClaveCredencial { get; set; }

    public bool TieneValorAdicional { get; set; }

    public bool OcultarClaveCredencial { get; set; }

    public bool OcultarValorAdicional { get; set; }

    public bool GuardadoEnBd { get; set; }

    public bool Activo { get; set; }

    public string? Error { get; set; }
}

public class SaludIntegracionGuardarRequest
{
    public string Id { get; init; } = string.Empty;

    public string? Endpoint { get; init; }

    public string? EndpointVerificacion { get; init; }

    public string? ClaveCredencial { get; init; }

    public string? ValorAdicional { get; init; }

    public string? Prompt { get; init; }

    public string? Descripcion { get; init; }
}
