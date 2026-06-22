using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;

namespace Infrastructure;

/// <summary>
/// Escucha ArchivosNuevos en UNC y procesa lotes TXT de uno en uno (RF-01, RF-02).
/// Usa sondeo con impersonación; FileSystemWatcher no es fiable en rutas UNC como servicio Windows.
/// </summary>
public class LoteWatcherInfrastructure
{
    private readonly RedDisponibleService _redDisponible;
    private readonly LoteProcesamientoService _loteProcesamiento;
    private readonly ILogger<LoteWatcherInfrastructure> _logger;
    private readonly string _directorioArchivosNuevos;
    private readonly int _escaneoSegundos;
    private CancellationToken _stoppingToken;

    public LoteWatcherInfrastructure(
        IOptions<RutasSettings> rutasOptions,
        IOptions<FileSettings> fileSettings,
        RedDisponibleService redDisponible,
        LoteProcesamientoService loteProcesamiento,
        ILogger<LoteWatcherInfrastructure> logger)
    {
        _redDisponible = redDisponible;
        _loteProcesamiento = loteProcesamiento;
        _logger = logger;
        _directorioArchivosNuevos = rutasOptions.Value.RutaArchivosNuevos;
        _escaneoSegundos = Math.Max(1, fileSettings.Value.ArchivosNuevosEscaneoSegundos);

        if (string.IsNullOrWhiteSpace(_directorioArchivosNuevos))
            throw new InvalidOperationException(
                "Modo Red requiere Rutas.RaizUnc configurado para resolver ArchivosNuevos.");
    }

    /// <summary>
    /// Ciclo principal: procesa el primer TXT pendiente, vuelve a listar de inmediato;
    /// si no hay más, espera y sigue escuchando ArchivosNuevos.
    /// </summary>
    public async Task EjecutarEscuchaAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        _logger.LogInformation(
            "LoteWatcherIniciado | Ruta={Ruta} | EscaneoSegundos={EscaneoSegundos}",
            _directorioArchivosNuevos,
            _escaneoSegundos);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!PuedeAccederArchivosNuevos())
                {
                    await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
                    continue;
                }

                if (await ProcesarSiguienteTxtSiExisteAsync())
                    continue;

                _logger.LogDebug(
                    "ArchivosNuevosVacio | Esperando nuevos TXT | Ruta={Ruta}",
                    _directorioArchivosNuevos);

                await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ciclo de escucha de ArchivosNuevos");
                await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
            }
        }

        _logger.LogInformation("LoteWatcherDetenido | Ruta={Ruta}", _directorioArchivosNuevos);
    }

    private bool PuedeAccederArchivosNuevos()
    {
        if (!_redDisponible.EstaDisponible())
        {
            _logger.LogWarning(
                "ArchivosNuevosNoAccesible | Ruta={Ruta} | Error={Error}",
                _directorioArchivosNuevos,
                _redDisponible.UltimoErrorMensaje);
            return false;
        }

        var carpetaExiste = _redDisponible.EjecutarConAcceso(() =>
            Directory.Exists(_directorioArchivosNuevos));

        if (carpetaExiste)
            return true;

        _logger.LogWarning(
            "ArchivosNuevosNoExiste | Ruta={Ruta} | Nota=La carpeta debe existir previamente en la UNC",
            _directorioArchivosNuevos);
        return false;
    }

    private async Task<bool> ProcesarSiguienteTxtSiExisteAsync()
    {
        var rutaTxt = ObtenerPrimerTxtPendiente();
        if (rutaTxt == null)
            return false;

        _logger.LogInformation(
            "TxtDetectado | Archivo={Archivo}",
            Path.GetFileName(rutaTxt));

        return await ProcesarTxtAsync(rutaTxt);
    }

    private string? ObtenerPrimerTxtPendiente()
    {
        if (!_redDisponible.EstaDisponible())
            return null;

        return _redDisponible.EjecutarConAcceso(() =>
        {
            if (!Directory.Exists(_directorioArchivosNuevos))
                return null;

            return Directory.GetFiles(_directorioArchivosNuevos, "*.txt")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        });
    }

    private async Task<bool> ProcesarTxtAsync(string rutaTxt)
    {
        try
        {
            await _redDisponible.EjecutarConAccesoAsync(async () =>
            {
                if (!File.Exists(rutaTxt))
                    return;

                await EsperarArchivoTxtDisponible(rutaTxt);
            });

            if (!_redDisponible.EjecutarConAcceso(() => File.Exists(rutaTxt)))
            {
                _logger.LogWarning(
                    "TxtNoDisponible | Archivo={Archivo}",
                    Path.GetFileName(rutaTxt));
                return false;
            }

            var resultado = await _loteProcesamiento.ProcesarLoteAsync(rutaTxt, _stoppingToken);

            _logger.LogInformation(
                "TxtProcesado | Archivo={Archivo} | Estado={Estado} | RevisandoArchivosNuevos={RevisandoArchivosNuevos}",
                Path.GetFileName(rutaTxt),
                resultado.Estado,
                resultado.PermiteContinuarInmediato);

            return resultado.PermiteContinuarInmediato;
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // cierre del servicio
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoLote | Txt={Txt}",
                Path.GetFileName(rutaTxt));
            return false;
        }
    }

    private async Task EsperarArchivoTxtDisponible(string rutaTxt)
    {
        for (int i = 0; i < 20; i++)
        {
            _stoppingToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(
                    rutaTxt,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                if (stream.Length > 0)
                    return;
            }
            catch (IOException)
            {
                // archivo aún bloqueado
            }

            await Task.Delay(250, _stoppingToken);
        }
    }
}
