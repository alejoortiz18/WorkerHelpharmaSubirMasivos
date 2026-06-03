using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly RutasSettings _rutas;

    public Worker(ILogger<Worker> logger, IOptions<RutasSettings> rutasOptions)
    {
        _logger = logger;
        _rutas = rutasOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MoverDocumentosIniciado");

        AsegurarCarpetaLocal();

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void AsegurarCarpetaLocal()
    {
        if (!Directory.Exists(_rutas.CarpetaLocal))
        {
            Directory.CreateDirectory(_rutas.CarpetaLocal);
            _logger.LogInformation(
                "CarpetaLocalCreada | Ruta={Ruta}",
                _rutas.CarpetaLocal);
        }

        _logger.LogInformation(
            "CarpetaLocalLista | Ruta={Ruta}",
            _rutas.CarpetaLocal);

        _logger.LogInformation(
            "RutasProduccion | RaizUnc={RaizUnc} | ArchivosNuevos={ArchivosNuevos}",
            _rutas.RaizUnc,
            _rutas.RutaArchivosNuevos);
    }
}
