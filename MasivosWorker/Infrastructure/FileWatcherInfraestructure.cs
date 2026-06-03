using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

namespace Infrastructure;

public class FileWatcherInfraestructure
{
    private readonly RutasSettings _rutas;
    private readonly ILogger<FileWatcherInfraestructure> _logger;
    private FileSystemWatcher _watcher;
    private readonly BarcodeRegionService _barcodeRegionService;
    private readonly FileManagerInfraestructure _fileManager;

    private readonly string _directorioEntrada;
    private readonly string _directorioEntradaNormalizado;
    private readonly string _directorioProcesandoNormalizado;

    /// <summary>Un turno por ruta: si llegan varios eventos del watcher, se encolan y todos ejecutan el flujo (no se descartan).</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _turnosPorArchivo =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _semaforo;
    private readonly int _maxReintentos;
    private readonly int _esperaMs;
    private readonly int _archivoEsperaIntentos;
    private readonly int _archivoEsperaMs;
    private readonly int _archivoLecturasEstables;
    private CancellationToken _stoppingToken;

    // CONTADORES
    private int _procesadosOk = 0;
    private int _procesadosError = 0;
    private int _procesadosTotal = 0;

    private readonly object _lockStats = new();

    private long _tiempoTotalProcesamientoMs = 0;
    private int _conteoTiempos = 0;

    private readonly SoporteProcesamientoService _soporteProcesamiento;


    public FileWatcherInfraestructure(
        IOptions<RutasSettings> rutasOptions,
        IOptions<FileSettings> fileSettings,
        ILogger<FileWatcherInfraestructure> logger,
        BarcodeRegionService baco,
        SoporteProcesamientoService soporteProcesamiento,
        FileManagerInfraestructure fileManager)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
        _barcodeRegionService = baco;
        _fileManager = fileManager;
        _soporteProcesamiento = soporteProcesamiento;
        _directorioEntrada = _rutas.Procesar;
        _directorioEntradaNormalizado = NormalizarDirectorio(_directorioEntrada);
        _directorioProcesandoNormalizado = NormalizarDirectorio(_rutas.Procesando);
        _semaforo = new SemaphoreSlim(fileSettings.Value.MaxArchivosConcurrentes);
        _maxReintentos = fileSettings.Value.BarcodeMaxReintentos;
        _esperaMs = fileSettings.Value.BarcodeEsperaMs;
        _archivoEsperaIntentos = Math.Max(1, fileSettings.Value.ArchivoEsperaIntentos);
        _archivoEsperaMs = Math.Max(100, fileSettings.Value.ArchivoEsperaMs);
        _archivoLecturasEstables = Math.Max(1, fileSettings.Value.ArchivoLecturasEstables);
        Directory.CreateDirectory(_directorioEntrada);
    }

    private static string NormalizarDirectorio(string ruta) =>
        Path.GetFullPath(ruta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public void Iniciar(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        _watcher = new FileSystemWatcher(_directorioEntrada)
        {
            Filter = "*.pdf",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536
        };

        _watcher.Created += (s, e) => EncolarArchivoDetectado(e.FullPath);
        _watcher.Changed += (s, e) => EncolarArchivoDetectado(e.FullPath);
        _watcher.Renamed += (s, e) => EncolarArchivoDetectado(e.FullPath);

        _watcher.Error += (s, e) =>
        {
            _logger.LogError(
                "FileSystemWatcherError | El buffer de eventos se desbordó; el escaneo periódico seguirá detectando archivos en Procesar.");
        };

        _logger.LogInformation(
            "Watcher iniciado en carpeta Procesar | Ruta={Ruta}",
            _directorioEntradaNormalizado);

        // Escaneo periódico: recupera archivos atascados o no detectados por el watcher
        // (redes, antivirus, copias desde /error, buffer overflow del watcher)
        _ = Task.Run(async () =>
        {
            await EscanearCarpetaEntradaAsync();

            while (!_stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _stoppingToken);
                    await EscanearCarpetaEntradaAsync();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en escaneo periódico de carpeta Procesar");
                }
            }
        });
    }

    private void EncolarArchivoDetectado(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return;

        Task.Run(() => ProcesarArchivoAsync(ruta));
    }

    private async Task EscanearCarpetaEntradaAsync()
    {
        if (!Directory.Exists(_directorioEntrada))
            return;

        var archivos = Directory.GetFiles(_directorioEntrada, "*.pdf");
        if (archivos.Length == 0)
            return;

        _logger.LogDebug(
            "EscaneoProcesar | ArchivosEncontrados={Cantidad}",
            archivos.Length);

        foreach (var archivo in archivos)
            _ = Task.Run(() => ProcesarArchivoAsync(archivo));
    }

    public void ProcesarPendientesAlIniciar(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        ProcesarPendientesEnCarpeta(
            _rutas.Procesando,
            yaEnProcesando: true,
            etiqueta: "PROCESANDO");

        ProcesarPendientesEnCarpeta(
            _directorioEntrada,
            yaEnProcesando: false,
            etiqueta: "PROCESAR");
    }

    private void ProcesarPendientesEnCarpeta(string carpeta, bool yaEnProcesando, string etiqueta)
    {
        try
        {
            if (!Directory.Exists(carpeta))
                return;

            var archivos = Directory.GetFiles(carpeta, "*.pdf");

            if (archivos.Length == 0)
            {
                _logger.LogInformation("No hay archivos pendientes en {Carpeta}", etiqueta);
                return;
            }

            _logger.LogWarning(
                "RecuperacionArchivos | Carpeta={Carpeta} | Cantidad={Cantidad}",
                etiqueta,
                archivos.Length);

            foreach (var archivo in archivos)
                Task.Run(() => ProcesarArchivoAsync(archivo, yaEnProcesando));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recuperando archivos pendientes en {Carpeta}", etiqueta);
        }
    }

    private async Task ProcesarArchivoAsync(string ruta, bool yaEnProcesando = false)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !ruta.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            ruta = Path.GetFullPath(ruta);
        }
        catch
        {
            return;
        }

        if (!File.Exists(ruta))
            return;

        if (!EsRutaEnCarpetaEsperada(ruta, yaEnProcesando))
        {
            _logger.LogDebug(
                "EventoIgnorado | Archivo={Archivo} | Motivo=FueraDeCarpetaEsperada | YaEnProcesando={YaEnProcesando}",
                Path.GetFileName(ruta),
                yaEnProcesando);
            return;
        }

        var nombreArchivo = Path.GetFileName(ruta);
        var turnoArchivo = ObtenerTurnoArchivo(ruta);
        var enCola = turnoArchivo.CurrentCount == 0;

        if (enCola)
        {
            _logger.LogInformation(
                "ArchivoEnCola | Archivo={Archivo} | Motivo=Otro hilo está procesando la misma ruta; se esperará y se ejecutará igual",
                nombreArchivo);
        }

        await turnoArchivo.WaitAsync(_stoppingToken);

        // El archivo puede haber sido movido por una ejecución previa encolada
        // para la misma ruta (evento duplicado Created/Changed/Renamed).
        // En ese caso no es error: se descarta esta ejecución silenciosamente.
        if (!File.Exists(ruta))
        {
            _logger.LogInformation(
                "ArchivoYaMovido | Archivo={Archivo} | Ruta={Ruta} | Nota=Evento duplicado; ya fue atendido por una ejecución previa",
                nombreArchivo,
                ruta);
            turnoArchivo.Release();
            return;
        }

        await _semaforo.WaitAsync(_stoppingToken);

        string rutaProcesando = null;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "ProcesamientoIniciado | Archivo={Archivo} | Ruta={Ruta} | Nota=No se valida historial previo; cada archivo en Procesar se ejecuta",
                nombreArchivo,
                ruta
            );

            await EsperarArchivoDisponible(ruta);

            // mover a procesando (si ya viene de esa carpeta, no mover)
            rutaProcesando = yaEnProcesando ? ruta : _fileManager.MoverAProcesando(ruta);

            // esperar que el archivo esté disponible en Procesando (el antivirus puede rescanearlo)
            await EsperarArchivoDisponible(rutaProcesando);

            var documento = await ProcesarConReintentos(rutaProcesando, nombreArchivo);

            if (documento != null)
            {
                var soporte = $"{documento.Prefijo}{documento.Numero}";

                var resultado = await _soporteProcesamiento.ProcesarAsync(soporte, rutaProcesando);

                if (!resultado.EsExitoso)
                {
                    _logger.LogError(
                        "FalloIntegracionSoporte | Archivo={Archivo} | Soporte={Soporte} | Estado={Estado}",
                        nombreArchivo,
                        soporte,
                        resultado.Estado);

                    _fileManager.MoverAError(rutaProcesando);
                    ActualizarContadores(ok: false);
                    return;
                }

                _fileManager.MoverAProcesados(rutaProcesando, documento.NombreArchivo);

                ActualizarContadores(ok: true);
            }
            else
            {
                _fileManager.MoverAError(rutaProcesando);

                _logger.LogWarning(
                    "ArchivoSinCodigo | Archivo={Archivo} | Ruta={Ruta}",
                    nombreArchivo,
                    rutaProcesando
                );

                ActualizarContadores(ok: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoArchivo | Archivo={Archivo} | Ruta={Ruta} | Mensaje={Mensaje}",
                nombreArchivo,
                rutaProcesando ?? ruta,
                ex.Message);

            try
            {
                if (!string.IsNullOrEmpty(rutaProcesando) && File.Exists(rutaProcesando))
                    _fileManager.MoverAError(rutaProcesando);
                else if (File.Exists(ruta))
                    _fileManager.MoverAErrorDesdeOrigen(ruta);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "ErrorMoverAError | Archivo={Archivo}", nombreArchivo);
            }

            ActualizarContadores(ok: false);
        }
        finally
        {
            stopwatch.Stop();

            lock (_lockStats)
            {
                _tiempoTotalProcesamientoMs += stopwatch.ElapsedMilliseconds;
                _conteoTiempos++;
            }

            _semaforo.Release();
            turnoArchivo.Release();
        }
    }

    private SemaphoreSlim ObtenerTurnoArchivo(string ruta) =>
        _turnosPorArchivo.GetOrAdd(ruta, _ => new SemaphoreSlim(1, 1));

    private void ActualizarContadores(bool ok)
    {
        lock (_lockStats)
        {
            _procesadosTotal++;

            if (ok)
                _procesadosOk++;
            else
                _procesadosError++;

            // 🔥 LOG RESUMEN CADA 10 ARCHIVOS
            if (_procesadosTotal % 10 == 0)
            {
                double promedio = _conteoTiempos > 0
                    ? (double)_tiempoTotalProcesamientoMs / _conteoTiempos
                    : 0;

                double tasaError = _procesadosTotal > 0
                    ? ((double)_procesadosError / _procesadosTotal) * 100
                    : 0;

                _logger.LogInformation(
                    "ResumenProcesamiento | Total={Total} | OK={OK} | Error={Error} | PromedioMs={Promedio} | ErrorPct={ErrorPct}",
                    _procesadosTotal,
                    _procesadosOk,
                    _procesadosError,
                    Math.Round(promedio, 2),
                    Math.Round(tasaError, 2)
                );
            }
        }
    }

    private async Task EsperarArchivoDisponible(string ruta)
    {
        long ultimaLongitud = -1;
        int lecturasEstables = 0;
        Exception? ultimoError = null;

        for (int i = 0; i < _archivoEsperaIntentos; i++)
        {
            _stoppingToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(ruta))
                {
                    ultimoError = new FileNotFoundException("El archivo no existe en disco.", ruta);
                    lecturasEstables = 0;
                    ultimaLongitud = -1;
                }
                else
                {
                    using var stream = AbrirArchivoParaLectura(ruta);
                    var longitud = stream.Length;

                    if (longitud <= 0)
                    {
                        ultimoError = new InvalidDataException("El archivo tiene tamaño cero.");
                        lecturasEstables = 0;
                        ultimaLongitud = -1;
                    }
                    else if (longitud == ultimaLongitud)
                    {
                        lecturasEstables++;
                        if (lecturasEstables >= _archivoLecturasEstables)
                        {
                            _logger.LogDebug(
                                "ArchivoDisponible | Archivo={Archivo} | Tamano={Tamano} | Intento={Intento}",
                                Path.GetFileName(ruta),
                                longitud,
                                i + 1);
                            return;
                        }
                    }
                    else
                    {
                        ultimaLongitud = longitud;
                        lecturasEstables = 1;
                        ultimoError = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ultimoError = ex;
                lecturasEstables = 0;
                ultimaLongitud = -1;
            }

            await Task.Delay(_archivoEsperaMs, _stoppingToken);
        }

        var detalle = ultimoError?.Message ?? "tiempo de espera agotado";
        throw new IOException(
            $"El archivo no está disponible tras {_archivoEsperaIntentos} intentos: {detalle}",
            ultimoError);
    }

    private static FileStream AbrirArchivoParaLectura(string ruta) =>
        new(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private async Task<DocumentoProcesadoDto?> ProcesarConReintentos(string ruta, string nombreArchivo)
    {
        for (int intento = 1; intento <= _maxReintentos; intento++)
        {
            try
            {
                var resultado = await Task.Run(() =>
                    _barcodeRegionService.ProcesarPdf(ruta)
                );

                if (resultado != null)
                {
                    if (intento > 1)
                    {
                        _logger.LogInformation(
                            "ReintentoExitoso | Archivo={Archivo} | Intento={Intento}",
                            nombreArchivo,
                            intento
                        );
                    }

                    return resultado;
                }

                _logger.LogWarning(
                    "IntentoFallido | Archivo={Archivo} | Intento={Intento}",
                    nombreArchivo,
                    intento
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ErrorEnIntento | Archivo={Archivo} | Intento={Intento}",
                    nombreArchivo,
                    intento
                );
            }

            // espera entre intentos
            await Task.Delay(_esperaMs, _stoppingToken);
        }

        return null;
    }

    private bool EsRutaEnCarpetaEsperada(string rutaCompleta, bool yaEnProcesando)
    {
        var directorio = NormalizarDirectorio(Path.GetDirectoryName(rutaCompleta)!);
        var esperado = yaEnProcesando
            ? _directorioProcesandoNormalizado
            : _directorioEntradaNormalizado;

        return string.Equals(directorio, esperado, StringComparison.OrdinalIgnoreCase);
    }
}