using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MasivosWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileManagerService _fileManager;
        private readonly FileWatcherService _watcher;

        public Worker(
            ILogger<Worker> logger,
            FileManagerService fileManager,
            FileWatcherService watcher)
        {
            _logger = logger;
            _fileManager = fileManager;
            _watcher = watcher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker iniciado");

            try
            {
                // 🔥 Paso 1: Crear estructura
                _fileManager.CrearCarpetasSiNoExisten();
                _fileManager.CrearAccesosDirectos();

                // 🔥 Paso 2: Iniciar watcher
                _watcher.Iniciar();

                _logger.LogInformation("Sistema listo y escuchando archivos...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inicializando el Worker");
                throw; // 🔥 importante: no ocultar errores críticos
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                // 🔥 Esto es normal cuando se detiene el servicio
            }

            _logger.LogInformation("Worker detenido");
        }
    }
}