using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

public class RegistroUsuarioService
{
    private static readonly object ArchivoLock = new();

    private readonly RutasSettings _rutas;
    private readonly ILogger<RegistroUsuarioService> _logger;
    private bool _usuarioRegistradoEnSesion;

    public RegistroUsuarioService(
        IOptions<RutasSettings> rutasOptions,
        ILogger<RegistroUsuarioService> logger)
    {
        _rutas = rutasOptions.Value;
        _logger = logger;
    }

    public void RegistrarSiNoExiste(string usuarioNormalizado)
    {
        if (_usuarioRegistradoEnSesion)
            return;

        var rutaArchivo = _rutas.RutaArchivoUsuarios;
        Directory.CreateDirectory(_rutas.RutaUsuarios);

        lock (ArchivoLock)
        {
            var usuariosExistentes = CargarUsuarios(rutaArchivo);
            if (usuariosExistentes.Contains(usuarioNormalizado))
            {
                _usuarioRegistradoEnSesion = true;
                return;
            }

            File.AppendAllText(rutaArchivo, usuarioNormalizado + Environment.NewLine);
            _usuarioRegistradoEnSesion = true;

            _logger.LogInformation(
                "UsuarioRegistrado | Usuario={Usuario}",
                usuarioNormalizado);
        }
    }

    private static HashSet<string> CargarUsuarios(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return File.ReadAllLines(rutaArchivo)
            .Select(l => l.Trim().ToLowerInvariant())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
