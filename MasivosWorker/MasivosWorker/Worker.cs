using Infrastructure;

namespace MasivosWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileManagerInfraestructure _fileManager;
        private readonly FileWatcherInfraestructure _watcher;

        public Worker(
            ILogger<Worker> logger,
            FileManagerInfraestructure fileManager,
            FileWatcherInfraestructure watcher)
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
               
                _fileManager.CrearCarpetasSiNoExisten();
                _fileManager.CrearAccesosDirectos();

                _watcher.ProcesarPendientesAlIniciar();
                _watcher.Iniciar();

                _logger.LogInformation("Sistema listo y escuchando archivos...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inicializando el Worker");
                throw; 
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
                
            }

            _logger.LogInformation("Worker detenido");
        }
    }
}