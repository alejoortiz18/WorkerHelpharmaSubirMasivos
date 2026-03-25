using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;
using Services;

namespace Infrastructure;

public class FileWatcherService
{
    private readonly RutasSettings _rutas;
    private readonly ILogger<FileWatcherService> _logger;
    private FileSystemWatcher _watcher;
    private readonly BarcodeRegionService _barcodeRegionService;

    private readonly string _directorioEntrada;
    private readonly string _directorioProcesados;
    private readonly string _directorioError;

    public FileWatcherService(
        IOptions<RutasSettings> rutasOptions,
        ILogger<FileWatcherService> logger,
        BarcodeRegionService baco)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
        _barcodeRegionService = baco;

        // 🔥 rutas desde configuración
        _directorioEntrada = _rutas.Procesar;
        _directorioProcesados = _rutas.Procesados;
        _directorioError = _rutas.Error;

        // 🔥 asegurar que existan
        Directory.CreateDirectory(_directorioEntrada);
        Directory.CreateDirectory(_directorioProcesados);
        Directory.CreateDirectory(_directorioError);
    }

    public void Iniciar()
    {
        _watcher = new FileSystemWatcher(_rutas.Procesar)
        {
            Filter = "*.pdf",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnCreated;

        _logger.LogInformation("Watcher iniciado en carpeta Procesar");
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation($"Procesando archivo: {e.FullPath}");

            Thread.Sleep(2000);

            var documento = _barcodeRegionService.ProcesarPdf(e.FullPath);

            if (documento != null)
            {
                var destino = Path.Combine(_directorioProcesados, documento.NombreArchivo);

                File.Move(e.FullPath, destino, true);

                _logger.LogInformation($"Procesado OK → {documento.NombreArchivo}");
            }
            else
            {
                var destino = Path.Combine(_directorioError, Path.GetFileName(e.FullPath));

                File.Move(e.FullPath, destino, true);

                _logger.LogWarning("Movido a ERROR (no se pudo leer)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando archivo");

            try
            {
                var destino = Path.Combine(_directorioError, Path.GetFileName(e.FullPath));
                File.Move(e.FullPath, destino, true);
            }
            catch { }
        }
    }
}