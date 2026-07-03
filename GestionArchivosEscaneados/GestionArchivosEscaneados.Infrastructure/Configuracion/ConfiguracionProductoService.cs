using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Configuracion;

public interface IConfiguracionProductoService
{
    Task<IReadOnlyList<ConfiguracionProducto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<ConfiguracionProducto?> ObtenerAsync(string producto, CancellationToken cancellationToken = default);

    Task GuardarAsync(ConfiguracionProducto configuracion, CancellationToken cancellationToken = default);

    Task SembrarDesdeAppSettingsSiFaltanAsync(CancellationToken cancellationToken = default);
}

public class ConfiguracionProductoService : IConfiguracionProductoService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfiguracionProductoService> _logger;

    public ConfiguracionProductoService(
        ITrazabilidadConsultaSqlService trazabilidad,
        IConfiguration configuration,
        ILogger<ConfiguracionProductoService> logger)
    {
        _trazabilidad = trazabilidad;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<IReadOnlyList<ConfiguracionProducto>> ListarAsync(CancellationToken cancellationToken = default) =>
        _trazabilidad.ListarConfiguracionesProductoAsync(cancellationToken);

    public Task<ConfiguracionProducto?> ObtenerAsync(string producto, CancellationToken cancellationToken = default) =>
        _trazabilidad.ObtenerConfiguracionProductoAsync(producto, cancellationToken);

    public async Task GuardarAsync(ConfiguracionProducto configuracion, CancellationToken cancellationToken = default)
    {
        await _trazabilidad.GuardarConfiguracionProductoAsync(configuracion, cancellationToken);
        _logger.LogInformation("ConfiguracionProductoGuardada | Producto={Producto}", configuracion.Producto);
    }

    public async Task SembrarDesdeAppSettingsSiFaltanAsync(CancellationToken cancellationToken = default)
    {
        foreach (var semilla in CrearSemillasDesdeAppSettings())
        {
            var existente = await ObtenerAsync(semilla.Producto, cancellationToken);
            if (existente is not null)
                continue;

            await GuardarAsync(semilla, cancellationToken);
            _logger.LogInformation("ConfiguracionProductoSembrada | Producto={Producto}", semilla.Producto);
        }
    }

    private IEnumerable<ConfiguracionProducto> CrearSemillasDesdeAppSettings()
    {
        yield return new ConfiguracionProducto
        {
            Producto = ProductoIntegracion.Unc,
            Endpoint = Config("Rutas:RaizUnc"),
            ClaveCredencial = Config("Red:Usuario"),
            ValorAdicional = Config("Red:Clave"),
            Descripcion = "Acceso a carpeta UNC / NAS"
        };

        yield return new ConfiguracionProducto
        {
            Producto = ProductoIntegracion.SoporteApi,
            Endpoint = Config("Integraciones:SoporteApiUrl") ?? IntegracionDefaults.SoporteApiUrl,
            ClaveCredencial = Config("ApiCredentials:SoporteApiKey"),
            Descripcion = "Consulta de datos de soportes"
        };

        yield return new ConfiguracionProducto
        {
            Producto = ProductoIntegracion.SoporteFisico,
            Endpoint = Config("Integraciones:SoporteFisicoApiUrl") ?? IntegracionDefaults.SoporteFisicoApiUrl,
            ClaveCredencial = Config("ApiCredentials:SoporteFisicoToken"),
            ValorAdicional = Config("ApiCredentials:IdUsuario"),
            Descripcion = "Envío de soporte físico a intranet"
        };

        yield return new ConfiguracionProducto
        {
            Producto = ProductoIntegracion.OpenAi,
            Endpoint = Config("Integraciones:OpenAiApiUrl") ?? IntegracionDefaults.OpenAiApiUrl,
            EndpointVerificacion = Config("Integraciones:OpenAiModelsUrl") ?? IntegracionDefaults.OpenAiModelsUrl,
            ClaveCredencial = Config("OpenAi:ApiKey"),
            ValorAdicional = Config("OpenAi:Model"),
            Descripcion = "Lectura de códigos de barras con OpenAI"
        };

        yield return new ConfiguracionProducto
        {
            Producto = ProductoIntegracion.RadicaWeb,
            Endpoint = Config("RadicaWeb:ApiUrl") ?? IntegracionDefaults.RadicaWebApiUrl,
            ClaveCredencial = Config("RadicaWeb:ApiClient"),
            ValorAdicional = Config("RadicaWeb:ApiSecret"),
            Descripcion = "Notificación de soportes físicos al API RadicaWeb"
        };
    }

    private string? Config(string clave)
    {
        var valor = _configuration[clave];
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
