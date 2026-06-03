using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

public class EstructuraCarpetasService
{
    private readonly RutasSettings _rutas;
    private readonly ILogger<EstructuraCarpetasService> _logger;

    public EstructuraCarpetasService(
        IOptions<RutasSettings> rutasOptions,
        ILogger<EstructuraCarpetasService> logger)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
    }

    public string CrearEstructuraDia(string usuario, DateOnly fecha)
    {
        var carpetaDia = Path.Combine(
            _rutas.RaizUnc,
            usuario.ToLowerInvariant(),
            fecha.ToString("yyyy-MM-dd"));

        foreach (var subcarpeta in _rutas.SubcarpetasDia)
        {
            var ruta = Path.Combine(carpetaDia, subcarpeta.ToLowerInvariant());
            if (!Directory.Exists(ruta))
            {
                Directory.CreateDirectory(ruta);
                _logger.LogInformation(
                    "EstructuraCreada | Ruta={Ruta}",
                    ruta);
            }
        }

        return _rutas.ObtenerRutaCarpetaProcesar(usuario, fecha);
    }

    public string ObtenerCarpetaProcesar(string usuario, DateOnly fecha) =>
        _rutas.ObtenerRutaCarpetaProcesar(usuario, fecha);
}
