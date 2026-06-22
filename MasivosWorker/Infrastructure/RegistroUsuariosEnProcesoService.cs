using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

/// <summary>
/// Tabla virtual en memoria que registra qué usuarios tienen un archivo en
/// procesamiento (RF-02). Mientras un usuario esté registrado, ningún otro
/// archivo del mismo usuario podrá asignarse a procesamiento.
/// </summary>
public class RegistroUsuariosEnProcesoService
{
    private readonly ConcurrentDictionary<string, byte> _usuariosActivos =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<RegistroUsuariosEnProcesoService> _logger;

    public RegistroUsuariosEnProcesoService(ILogger<RegistroUsuariosEnProcesoService> logger)
    {
        _logger = logger;
    }

    /// <summary>Indica si el usuario ya tiene un archivo en procesamiento.</summary>
    public bool EstaActivo(string usuario) =>
        _usuariosActivos.ContainsKey(usuario);

    /// <summary>
    /// Registra al usuario si no estaba activo. Devuelve <c>true</c> cuando el
    /// archivo puede asignarse a un hilo; <c>false</c> si el usuario ya está en proceso.
    /// </summary>
    public bool IntentarRegistrar(string usuario)
    {
        var registrado = _usuariosActivos.TryAdd(usuario, 0);

        if (registrado)
            _logger.LogDebug(
                "UsuarioRegistradoEnProceso | Usuario={Usuario} | ActivosAhora={Activos}",
                usuario,
                _usuariosActivos.Count);

        return registrado;
    }

    /// <summary>Libera la tabla virtual del usuario al terminar su archivo (RF-05).</summary>
    public void Liberar(string usuario)
    {
        if (_usuariosActivos.TryRemove(usuario, out _))
            _logger.LogDebug(
                "UsuarioLiberado | Usuario={Usuario} | ActivosAhora={Activos}",
                usuario,
                _usuariosActivos.Count);
    }

    public int Activos => _usuariosActivos.Count;
}
