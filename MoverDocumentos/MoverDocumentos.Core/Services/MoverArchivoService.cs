using Microsoft.Extensions.Logging;

namespace MoverDocumentos.Core.Services;

public class MoverArchivoService
{
    private readonly ILogger<MoverArchivoService> _logger;

    public MoverArchivoService(ILogger<MoverArchivoService> logger)
    {
        _logger = logger;
    }

    public string Mover(string rutaOrigen, string carpetaDestinoProcesar)
    {
        Directory.CreateDirectory(carpetaDestinoProcesar);

        var nombre = Path.GetFileName(rutaOrigen);
        var destino = ResolverRutaDestinoSinSobrescribir(carpetaDestinoProcesar, nombre);

        if (!string.Equals(nombre, Path.GetFileName(destino), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "ArchivoRenombradoDuplicado | Nombre={Nombre}",
                Path.GetFileName(destino));
        }

        File.Move(rutaOrigen, destino);

        _logger.LogInformation(
            "ArchivoMovido | Origen={Origen} | Destino={Destino}",
            rutaOrigen,
            destino);

        return destino;
    }

    public static string ResolverRutaDestinoSinSobrescribir(string carpetaDestino, string nombreArchivo)
    {
        var destino = Path.Combine(carpetaDestino, nombreArchivo);
        if (!File.Exists(destino))
            return destino;

        var nombreSinExt = Path.GetFileNameWithoutExtension(nombreArchivo);
        var extension = Path.GetExtension(nombreArchivo);

        for (var i = 1; i < int.MaxValue; i++)
        {
            var candidato = Path.Combine(carpetaDestino, $"{nombreSinExt}({i}){extension}");
            if (!File.Exists(candidato))
                return candidato;
        }

        throw new IOException($"No se encontró nombre libre para {nombreArchivo} en {carpetaDestino}.");
    }
}
