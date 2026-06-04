using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Auth;

public class UsuarioAuthService
{
    private readonly RutasSettings _rutas;
    private readonly ILogger<UsuarioAuthService> _logger;

    public UsuarioAuthService(IOptions<RutasSettings> rutas, ILogger<UsuarioAuthService> logger)
    {
        _rutas = rutas.Value;
        _logger = logger;
    }

    public async Task<string?> ValidarYObtenerUsuarioNormalizadoAsync(
        string usuarioIngresado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioIngresado))
            return null;

        var ruta = _rutas.RutaArchivoUsuarios;
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            _logger.LogWarning("UsuariosTxtNoEncontrado | Ruta={Ruta}", ruta);
            return null;
        }

        var ingresoUpper = usuarioIngresado.Trim().ToUpperInvariant();
        var lineas = await File.ReadAllLinesAsync(ruta, cancellationToken);

        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            if (string.Equals(linea.Trim().ToUpperInvariant(), ingresoUpper, StringComparison.Ordinal))
                return linea.Trim().ToLowerInvariant();
        }

        return null;
    }
}
