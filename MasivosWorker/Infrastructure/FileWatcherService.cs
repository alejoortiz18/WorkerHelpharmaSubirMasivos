using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Infrastructure;

public class FileWatcherService
{
    private readonly RutasSettings _rutas;
    private readonly ILogger<FileWatcherService> _logger;
    private FileSystemWatcher _watcher;

    public FileWatcherService(
        IOptions<RutasSettings> rutasOptions,
        ILogger<FileWatcherService> logger)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
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
            _logger.LogInformation($"Nuevo archivo detectado: {e.FullPath}");

            // Aquí luego vamos a procesar el PDF
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar archivo");
        }
    }
}