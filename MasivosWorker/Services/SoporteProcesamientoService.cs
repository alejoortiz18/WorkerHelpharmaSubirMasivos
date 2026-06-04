using Microsoft.Extensions.Logging;
using Models.Dto;

namespace Services;

/// <summary>
/// Orquesta el mismo flujo de APIs que usa MasivosWorker al procesar un PDF con código de barras conocido:
/// 1) POST DatosSoportes con el código
/// 2) POST soporte/fisico con datos + PDF completo
/// El portal MVC debe usar esta clase (no reimplementar las llamadas HTTP).
/// </summary>
public class SoporteProcesamientoService : ISoporteProcesamientoService
{
    private readonly SoporteApiService _soporteApi;
    private readonly SoporteFisicoApiService _soporteFisicoApi;
    private readonly ILogger<SoporteProcesamientoService> _logger;

    public SoporteProcesamientoService(
        SoporteApiService soporteApi,
        SoporteFisicoApiService soporteFisicoApi,
        ILogger<SoporteProcesamientoService> logger)
    {
        _soporteApi = soporteApi;
        _soporteFisicoApi = soporteFisicoApi;
        _logger = logger;
    }

    /// <summary>
    /// Envía el código de barras a la API de datos y, si responde OK, adjunta el PDF en la API física.
    /// </summary>
    /// <param name="soporte">Código de barras (ej. KV351697).</param>
    /// <param name="rutaArchivoPdf">Ruta absoluta al PDF; se envía completo en el multipart.</param>
    public async Task<SoporteProcesamientoResult> ProcesarAsync(
        string soporte,
        string rutaArchivoPdf,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var respuesta = await _soporteApi.EnviarSoporteAsync(soporte);

        if (respuesta == null)
        {
            _logger.LogError(
                "FalloApiDatos | Soporte={Soporte} | Ruta={Ruta}",
                soporte,
                rutaArchivoPdf);

            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiDatos,
                Soporte = soporte
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var enviadoFisico = await _soporteFisicoApi.EnviarSoporteFisicoAsync(
            soporte,
            rutaArchivoPdf,
            respuesta);

        if (!enviadoFisico)
        {
            _logger.LogError(
                "FalloApiFisico | Soporte={Soporte} | Ruta={Ruta}",
                soporte,
                rutaArchivoPdf);

            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiFisico,
                Soporte = soporte,
                Datos = respuesta
            };
        }

        _logger.LogInformation(
            "SoporteProcesamientoOK | Soporte={Soporte} | Paciente={Paciente}",
            soporte,
            respuesta.NombrePaciente);

        return new SoporteProcesamientoResult
        {
            Estado = SoporteProcesamientoEstado.Exito,
            Soporte = soporte,
            Datos = respuesta
        };
    }
}
