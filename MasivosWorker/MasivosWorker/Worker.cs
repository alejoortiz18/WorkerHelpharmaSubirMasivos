using Infrastructure;

namespace MasivosWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly LoteWatcherInfrastructure _loteWatcher;

        public Worker(
            ILogger<Worker> logger,
            LoteWatcherInfrastructure loteWatcher)
        {
            _logger = logger;
            _loteWatcher = loteWatcher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker iniciado — escuchando lotes TXT en ArchivosNuevos");

            try
            {
                await _loteWatcher.EjecutarEscuchaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo de escucha de lotes");
                throw;
            }
        }
    }
}
