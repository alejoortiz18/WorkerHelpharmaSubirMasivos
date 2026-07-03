using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Models.Entities;
using Microsoft.Extensions.Configuration;

namespace GestionArchivosEscaneados.Infrastructure.Configuracion;

public interface IIntegracionConfigProvider
{
    Task<ConfiguracionProducto?> ObtenerProductoAsync(
        string producto,
        CancellationToken cancellationToken = default);

    Task GuardarProductoAsync(
        ConfiguracionProducto configuracion,
        CancellationToken cancellationToken = default);

    string ObtenerFallback(string clave);
}

public class IntegracionConfigProvider : IIntegracionConfigProvider
{
    private readonly IConfiguracionProductoService _productos;
    private readonly IConfiguration _configuration;

    public IntegracionConfigProvider(
        IConfiguracionProductoService productos,
        IConfiguration configuration)
    {
        _productos = productos;
        _configuration = configuration;
    }

    public Task<ConfiguracionProducto?> ObtenerProductoAsync(
        string producto,
        CancellationToken cancellationToken = default) =>
        _productos.ObtenerAsync(producto, cancellationToken);

    public Task GuardarProductoAsync(
        ConfiguracionProducto configuracion,
        CancellationToken cancellationToken = default) =>
        _productos.GuardarAsync(configuracion, cancellationToken);

    public string ObtenerFallback(string clave) =>
        _configuration[clave]?.Trim() ?? string.Empty;
}

public static class IntegracionConfigExtensions
{
    public static async Task<string> ObtenerRaizUncAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(provider, ProductoIntegracion.Unc, p => p.Endpoint, "Rutas:RaizUnc", cancellationToken);

    public static async Task<string> ObtenerRedUsuarioAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(provider, ProductoIntegracion.Unc, p => p.ClaveCredencial, "Red:Usuario", cancellationToken);

    public static async Task<string> ObtenerRedClaveAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(provider, ProductoIntegracion.Unc, p => p.ValorAdicional, "Red:Clave", cancellationToken);

    public static async Task<bool> UsaCredencialesUncAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default)
    {
        var usuario = await provider.ObtenerRedUsuarioAsync(cancellationToken);
        var clave = await provider.ObtenerRedClaveAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(clave))
            return true;

        var flag = provider.ObtenerFallback("Red:UsarCredencialesConfiguradas");
        return bool.TryParse(flag, out var usar) && usar;
    }

    public static async Task<string> ObtenerSoporteApiUrlAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.SoporteApi,
            p => p.Endpoint,
            "Integraciones:SoporteApiUrl",
            cancellationToken) is { Length: > 0 } url
            ? url
            : IntegracionDefaults.SoporteApiUrl;

    public static async Task<string> ObtenerSoporteApiKeyAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.SoporteApi,
            p => p.ClaveCredencial,
            "ApiCredentials:SoporteApiKey",
            cancellationToken);

    public static async Task<string> ObtenerSoporteFisicoApiUrlAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.SoporteFisico,
            p => p.Endpoint,
            "Integraciones:SoporteFisicoApiUrl",
            cancellationToken) is { Length: > 0 } url
            ? url
            : IntegracionDefaults.SoporteFisicoApiUrl;

    public static async Task<string> ObtenerSoporteFisicoTokenAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.SoporteFisico,
            p => p.ClaveCredencial,
            "ApiCredentials:SoporteFisicoToken",
            cancellationToken);

    public static async Task<string> ObtenerIdUsuarioSoporteFisicoAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.SoporteFisico,
            p => p.ValorAdicional,
            "ApiCredentials:IdUsuario",
            cancellationToken);

    public static async Task<string> ObtenerOpenAiApiUrlAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.OpenAi,
            p => p.Endpoint,
            "Integraciones:OpenAiApiUrl",
            cancellationToken) is { Length: > 0 } url
            ? url
            : IntegracionDefaults.OpenAiApiUrl;

    public static async Task<string> ObtenerOpenAiModelsUrlAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.OpenAi,
            p => p.EndpointVerificacion,
            "Integraciones:OpenAiModelsUrl",
            cancellationToken) is { Length: > 0 } url
            ? url
            : IntegracionDefaults.OpenAiModelsUrl;

    public static async Task<string> ObtenerOpenAiApiKeyAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.OpenAi,
            p => p.ClaveCredencial,
            "OpenAi:ApiKey",
            cancellationToken);

    public static async Task<string> ObtenerOpenAiModelAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.OpenAi,
            p => p.ValorAdicional,
            "OpenAi:Model",
            cancellationToken);

    public static async Task<string> ObtenerOpenAiPromptAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.OpenAi,
            p => p.Prompt,
            null,
            cancellationToken);

    public static async Task<string> ObtenerRadicaWebApiUrlAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.RadicaWeb,
            p => p.Endpoint,
            "RadicaWeb:ApiUrl",
            cancellationToken) is { Length: > 0 } url
            ? url
            : IntegracionDefaults.RadicaWebApiUrl;

    public static async Task<string> ObtenerRadicaWebApiClientAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.RadicaWeb,
            p => p.ClaveCredencial,
            "RadicaWeb:ApiClient",
            cancellationToken);

    public static async Task<string> ObtenerRadicaWebApiSecretAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default) =>
        await CampoProductoAsync(
            provider,
            ProductoIntegracion.RadicaWeb,
            p => p.ValorAdicional,
            "RadicaWeb:ApiSecret",
            cancellationToken);

    public static async Task<bool> RadicaWebEstaConfiguradoAsync(
        this IIntegracionConfigProvider provider,
        CancellationToken cancellationToken = default)
    {
        var client = await provider.ObtenerRadicaWebApiClientAsync(cancellationToken);
        var secret = await provider.ObtenerRadicaWebApiSecretAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(client) && !string.IsNullOrWhiteSpace(secret);
    }

    private static async Task<string> CampoProductoAsync(
        IIntegracionConfigProvider provider,
        string producto,
        Func<ConfiguracionProducto, string?> selector,
        string? claveFallback,
        CancellationToken cancellationToken)
    {
        var registro = await provider.ObtenerProductoAsync(producto, cancellationToken);
        var valor = registro is null ? null : selector(registro)?.Trim();
        if (!string.IsNullOrWhiteSpace(valor))
            return valor;

        return string.IsNullOrWhiteSpace(claveFallback)
            ? string.Empty
            : provider.ObtenerFallback(claveFallback);
    }
}
