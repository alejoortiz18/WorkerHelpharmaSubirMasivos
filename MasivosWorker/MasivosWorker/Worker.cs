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
                _loteWatcher.ProcesarPendientesAlIniciar(stoppingToken);
                _loteWatcher.Iniciar(stoppingToken);

                _logger.LogInformation("Sistema listo y escuchando archivos de lote...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inicializando el sistema");
                return;
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
