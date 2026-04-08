using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    private readonly HashSet<string> _procesando = new();
    private readonly SemaphoreSlim _semaforo = new(2);

    // 🔥 CONTADORES
    private int _procesadosOk = 0;
    private int _procesadosError = 0;
    private int _procesadosTotal = 0;

    private readonly object _lockStats = new();

    public FileWatcherInfraestructure(
        IOptions<RutasSettings> rutasOptions,
        ILogger<FileWatcherInfraestructure> logger,
        BarcodeRegionService baco,
        FileManagerInfraestructure fileManager)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
        _barcodeRegionService = baco;
        _fileManager = fileManager;

        _directorioEntrada = _rutas.Procesar;

        Directory.CreateDirectory(_directorioEntrada);
    }

    public void Iniciar()
    {
        _watcher = new FileSystemWatcher(_directorioEntrada)
        {
            Filter = "*.pdf",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += (s, e) =>
        {
            Task.Run(() => ProcesarArchivoAsync(e.FullPath));
        };

        _logger.LogInformation("Watcher iniciado en carpeta Procesar");
    }

    public void ProcesarPendientesAlIniciar()
    {
        try
        {
            var archivos = Directory.GetFiles(_rutas.Procesando, "*.pdf");

            if (archivos.Length == 0)
            {
                _logger.LogInformation("No hay archivos pendientes en PROCESANDO");
                return;
            }

            _logger.LogWarning(
                "RecuperacionArchivos | Cantidad={Cantidad}",
                archivos.Length
            );

            foreach (var archivo in archivos)
            {
                Task.Run(() => ProcesarArchivoAsync(archivo));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recuperando archivos pendientes");
        }
    }

    private async Task ProcesarArchivoAsync(string ruta)
    {
        await _semaforo.WaitAsync();

        var nombreArchivo = Path.GetFileName(ruta);

        if (!MarcarComoProcesando(ruta))
        {
            _semaforo.Release();
            return;
        }

        string rutaProcesando = null;

        try
        {
            _logger.LogInformation(
                "ArchivoDetectado | Archivo={Archivo} | Ruta={Ruta}",
                nombreArchivo,
                ruta
            );

            await EsperarArchivoDisponible(ruta);

            rutaProcesando = _fileManager.MoverAProcesando(ruta);

            //var documento = await Task.Run(() =>
            //    _barcodeRegionService.ProcesarPdf(rutaProcesando)
            //);

            var documento = await ProcesarConReintentos(rutaProcesando, nombreArchivo);

            if (documento != null)
            {
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
                ex.Message
            );

            try
            {
                if (!string.IsNullOrEmpty(rutaProcesando) && File.Exists(rutaProcesando))
                    _fileManager.MoverAError(rutaProcesando);
                else if (File.Exists(ruta))
                    _fileManager.MoverAError(ruta);
            }
            catch { }

            ActualizarContadores(ok: false);
        }
        finally
        {
            lock (_procesando)
            {
                _procesando.Remove(ruta);
            }

            _semaforo.Release();
        }
    }

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
                _logger.LogInformation(
                    "ResumenProcesamiento | Total={Total} | OK={OK} | Error={Error}",
                    _procesadosTotal,
                    _procesadosOk,
                    _procesadosError
                );
            }
        }
    }

    private async Task EsperarArchivoDisponible(string ruta)
    {
        int intentos = 10;

        for (int i = 0; i < intentos; i++)
        {
            try
            {
                using (FileStream stream = File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(500);
            }
        }

        throw new Exception("El archivo no está disponible");
    }

    private bool MarcarComoProcesando(string ruta)
    {
        lock (_procesando)
        {
            if (_procesando.Contains(ruta))
                return false;

            _procesando.Add(ruta);
            return true;
        }
    }

    private async Task<DocumentoProcesadoDto?> ProcesarConReintentos(string ruta, string nombreArchivo)
    {
        int maxIntentos = 3;

        for (int intento = 1; intento <= maxIntentos; intento++)
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

            // 🔥 pequeña espera entre intentos
            await Task.Delay(500);
        }

        return null;
    }
}