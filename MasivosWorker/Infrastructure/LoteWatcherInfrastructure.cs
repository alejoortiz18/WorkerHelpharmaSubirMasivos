using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Infrastructure;

/// <summary>
/// Escucha archivos TXT en ArchivosNuevos y procesa lotes de forma secuencial (RF-01, RF-02).
/// </summary>
public class LoteWatcherInfrastructure
{
    private readonly RutasSettings _rutas;
    private readonly LoteProcesamientoService _loteProcesamiento;
    private readonly ILogger<LoteWatcherInfrastructure> _logger;
    private readonly SemaphoreSlim _colaLotes = new(1, 1);
    private readonly string _directorioArchivosNuevos;
    private FileSystemWatcher? _watcher;
    private CancellationToken _stoppingToken;

    public LoteWatcherInfrastructure(
        IOptions<RutasSettings> rutasOptions,
        LoteProcesamientoService loteProcesamiento,
        ILogger<LoteWatcherInfrastructure> logger)
    {
        _rutas = rutasOptions.Value;
        _loteProcesamiento = loteProcesamiento;
        _logger = logger;
        _directorioArchivosNuevos = _rutas.RutaArchivosNuevos;

        if (string.IsNullOrWhiteSpace(_directorioArchivosNuevos))
            throw new InvalidOperationException(
                "Modo Red requiere Rutas.RaizUnc configurado para resolver ArchivosNuevos.");
    }

    public void Iniciar(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        if (!Directory.Exists(_directorioArchivosNuevos))
        {
            _logger.LogError(
                "ArchivosNuevosNoExiste | Ruta={Ruta} | Nota=La carpeta debe existir previamente en la UNC",
                _directorioArchivosNuevos);
            return;
        }

        _watcher = new FileSystemWatcher(_directorioArchivosNuevos)
        {
            Filter = "*.txt",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536
        };

        _watcher.Created += (_, e) => EncolarTxt(e.FullPath);
        _watcher.Changed += (_, e) => EncolarTxt(e.FullPath);
        _watcher.Renamed += (_, e) => EncolarTxt(e.FullPath);

        _watcher.Error += (_, _) =>
        {
            _logger.LogError(
                "LoteWatcherError | El buffer de eventos se desbordó; el escaneo periódico seguirá detectando TXT.");
        };

        _logger.LogInformation(
            "LoteWatcherIniciado | Ruta={Ruta}",
            _directorioArchivosNuevos);

        _ = Task.Run(async () =>
        {
            await EscanearTxtPendientesAsync();

            while (!_stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _stoppingToken);
                    await EscanearTxtPendientesAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en escaneo periódico de ArchivosNuevos");
                }
            }
        }, _stoppingToken);
    }

    public void ProcesarPendientesAlIniciar(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        if (!Directory.Exists(_directorioArchivosNuevos))
            return;

        var archivos = Directory.GetFiles(_directorioArchivosNuevos, "*.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archivos.Count == 0)
        {
            _logger.LogInformation("No hay TXT pendientes en ArchivosNuevos");
            return;
        }

        _logger.LogWarning(
            "RecuperacionLotes | Cantidad={Cantidad}",
            archivos.Count);

        foreach (var archivo in archivos)
            ProcesarTxtEnColaAsync(archivo).GetAwaiter().GetResult();
    }

    private void EncolarTxt(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) ||
            !ruta.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return;

        _ = Task.Run(() => ProcesarTxtEnColaAsync(ruta));
    }

    private async Task EscanearTxtPendientesAsync()
    {
        if (!Directory.Exists(_directorioArchivosNuevos))
            return;

        var archivos = Directory.GetFiles(_directorioArchivosNuevos, "*.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archivos.Count == 0)
            return;

        _logger.LogDebug(
            "EscaneoArchivosNuevos | Cantidad={Cantidad}",
            archivos.Count);

        foreach (var archivo in archivos)
            await ProcesarTxtEnColaAsync(archivo);
    }

    private async Task ProcesarTxtEnColaAsync(string rutaTxt)
    {
        await _colaLotes.WaitAsync(_stoppingToken);

        try
        {
            if (!File.Exists(rutaTxt))
                return;

            await EsperarArchivoTxtDisponible(rutaTxt);

            await _loteProcesamiento.ProcesarLoteAsync(rutaTxt, _stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // cierre del servicio
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoLote | Txt={Txt}",
                Path.GetFileName(rutaTxt));
        }
        finally
        {
            _colaLotes.Release();
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
