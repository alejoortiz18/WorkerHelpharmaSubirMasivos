using Infrastructure;

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
         
            _fileManager.CrearCarpetasSiNoExisten();
            
            _fileManager.CrearAccesosDirectos();

            _watcher.Iniciar();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
