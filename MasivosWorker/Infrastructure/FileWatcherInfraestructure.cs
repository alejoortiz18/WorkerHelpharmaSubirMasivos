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

    public FileWatcherInfraestructure(
        IOptions<RutasSettings> rutasOptions,
        ILogger<FileWatcherInfraestructure> logger,
        BarcodeRegionService baco,
        FileManagerInfraestructure fileManager) // 🔥 INYECCIÓN
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

    private async Task ProcesarArchivoAsync(string ruta)
    {
        await _semaforo.WaitAsync();

        if (!MarcarComoProcesando(ruta))
        {
            _semaforo.Release();
            return;
        }

        string rutaProcesando = null;

        try
        {
            _logger.LogInformation($"Archivo detectado: {ruta}");

            // 🔥 1. Esperar a que termine de copiarse
            await EsperarArchivoDisponible(ruta);

            // 🔥 2. MOVER A PROCESANDO (CLAVE)
            rutaProcesando = _fileManager.MoverAProcesando(ruta);

            _logger.LogInformation($"Procesando desde: {rutaProcesando}");

            // 🔥 3. PROCESAR
            var documento = await Task.Run(() =>
                _barcodeRegionService.ProcesarPdf(rutaProcesando)
            );

            // 🔥 4. RESULTADO
            if (documento != null)
            {
                _fileManager.MoverAProcesados(rutaProcesando, documento.NombreArchivo);

                _logger.LogInformation($"Procesado OK → {documento.NombreArchivo}");
            }
            else
            {
                _fileManager.MoverAError(rutaProcesando);

                _logger.LogWarning("No se pudo leer → enviado a ERROR");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error procesando archivo: {ruta}");

            try
            {
                if (!string.IsNullOrEmpty(rutaProcesando) && File.Exists(rutaProcesando))
                {
                    _fileManager.MoverAError(rutaProcesando);
                }
                else if (File.Exists(ruta))
                {
                    _fileManager.MoverAError(ruta);
                }
            }
            catch { }
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
}