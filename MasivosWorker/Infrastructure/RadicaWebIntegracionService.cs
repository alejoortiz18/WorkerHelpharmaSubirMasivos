using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;
using Services;

namespace Infrastructure;

public class RadicaWebIntegracionService : IRadicaWebIntegracionService
{
    private readonly RadicaWebApiService _api;
    private readonly ITrazabilidadSqlService _trazabilidad;
    private readonly RadicaWebSettings _settings;
    private readonly ILogger<RadicaWebIntegracionService> _logger;

    public RadicaWebIntegracionService(
        RadicaWebApiService api,
        ITrazabilidadSqlService trazabilidad,
        IOptions<RadicaWebSettings> settings,
        ILogger<RadicaWebIntegracionService> logger)
    {
        _api = api;
        _trazabilidad = trazabilidad;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcesarCombinacionesLoteAsync(
        RutasLoteContext contexto,
        IReadOnlyList<(DateOnly Fecha, string Bodega)> combinaciones,
        CancellationToken cancellationToken = default)
    {
        if (combinaciones.Count == 0)
        {
            _logger.LogInformation(
                "RadicaWebOmitido | Usuario={Usuario} | Fecha={Fecha} | Motivo=SinCombinaciones",
                contexto.Usuario,
                contexto.Fecha);
            return;
        }

        if (!_settings.EstaConfigurado)
        {
            _logger.LogWarning(
                "RadicaWebOmitido | Usuario={Usuario} | Fecha={Fecha} | Motivo=CredencialesNoConfiguradas | Combinaciones={Combinaciones}",
                contexto.Usuario,
                contexto.Fecha,
                combinaciones.Count);
            return;
        }

        _logger.LogInformation(
            "RadicaWebLoteIniciado | Usuario={Usuario} | Fecha={Fecha} | Combinaciones={Combinaciones}",
            contexto.Usuario,
            contexto.Fecha,
            combinaciones.Count);

        foreach (var (fecha, bodega) in combinaciones)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultado = await _api.EnviarBusquedaAsync(fecha, bodega, cancellationToken);

            try
            {
                await _trazabilidad.RegistrarRadicaWebAsync(
                    contexto,
                    fecha,
                    bodega,
                    resultado,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "RadicaWebTrazabilidadError | Usuario={Usuario} | FechaFactura={FechaFactura} | Bodega={Bodega}",
                    contexto.Usuario,
                    fecha,
                    bodega);
            }
        }

        _logger.LogInformation(
            "RadicaWebLoteFinalizado | Usuario={Usuario} | Fecha={Fecha} | Combinaciones={Combinaciones}",
            contexto.Usuario,
            contexto.Fecha,
            combinaciones.Count);
    }
}
