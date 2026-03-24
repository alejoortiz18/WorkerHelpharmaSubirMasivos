using Infrastructure;

namespace MasivosWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileManagerService _fileManager;

        public Worker(
            ILogger<Worker> logger,
            FileManagerService fileManager)
        {
            _logger = logger;
            _fileManager = fileManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker iniciado");

            // 👇 Crear carpetas al iniciar
            _fileManager.CrearCarpetasSiNoExisten();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
