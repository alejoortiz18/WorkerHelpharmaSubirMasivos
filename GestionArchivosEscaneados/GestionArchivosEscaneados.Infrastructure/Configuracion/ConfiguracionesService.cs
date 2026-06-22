using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Configuracion;

public interface IConfiguracionesService
{
    /// <summary>
    /// Obtiene el valor de una configuración desde BD.
    /// Si no existe, devuelve el valor por defecto.
    /// </summary>
    Task<string> ObtenerValorAsync(
        string clave,
        string valorPorDefecto = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda o actualiza una configuración en BD.
    /// </summary>
    Task GuardarAsync(
        string clave,
        string valor,
        string? descripcion = null,
        CancellationToken cancellationToken = default);
}

public class ConfiguracionesService : IConfiguracionesService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;
    private readonly ILogger<ConfiguracionesService> _logger;

    public ConfiguracionesService(
        ITrazabilidadConsultaSqlService trazabilidad,
        ILogger<ConfiguracionesService> logger)
    {
        _trazabilidad = trazabilidad;
        _logger = logger;
    }

    public async Task<string> ObtenerValorAsync(
        string clave,
        string valorPorDefecto = "",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var valor = await _trazabilidad.ObtenerConfiguracionAsync(clave, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(valor))
            {
                _logger.LogDebug(
                    "ConfiguracionNoEncontrada | Clave={Clave} | UsandoValorPorDefecto",
                    clave);
                return valorPorDefecto;
            }

            _logger.LogDebug("ConfiguracionObtenida | Clave={Clave}", clave);
            return valor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ErrorObteniendoConfiguracion | Clave={Clave}", clave);
            return valorPorDefecto;
        }
    }

    public async Task GuardarAsync(
        string clave,
        string valor,
        string? descripcion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _trazabilidad.GuardarConfiguracionAsync(clave, valor, descripcion, cancellationToken);
            _logger.LogInformation(
                "ConfiguracionGuardada | Clave={Clave} | Descripcion={Descripcion}",
                clave,
                descripcion ?? "-");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ErrorGuardandoConfiguracion | Clave={Clave}", clave);
            throw;
        }
    }
}
