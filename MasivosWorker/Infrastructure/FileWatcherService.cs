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
    private readonly BarcodeService _barcodeService;
    private readonly BarcodeRegionService _barcodeRegionService;


    public FileWatcherService(
    IOptions<RutasSettings> rutasOptions,
    ILogger<FileWatcherService> logger,
    BarcodeRegionService baco,
    BarcodeService barcodeService)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
        _barcodeService = barcodeService;
        _barcodeRegionService = baco;
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

        lbEscaneaDocs.Clases.LeeCodigoBarrasIronBar leer;
           
        try
        {
            _logger.LogInformation($"Procesando archivo: {e.FullPath}");

            // Esperar a que termine de copiarse
            Thread.Sleep(2000);
            
           // leer = new lbEscaneaDocs.Clases.LeeCodigoBarrasIronBar(e.FullPath);

           // var lecod = leer.ReaderBarCode();

            //var codigo = _barcodeService.ObtenerCodigo(e.FullPath);

            var codigo = _barcodeRegionService.LeerCodigoDesdePdf(e.FullPath);

           
            
           if (!string.IsNullOrEmpty(codigo))
            {
                _logger.LogInformation($"Código final: {codigo}");
            }
            else
            {
                _logger.LogWarning("Archivo sin código válido");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando archivo");
        }
    }
}