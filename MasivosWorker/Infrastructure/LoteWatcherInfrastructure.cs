using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;

namespace Infrastructure;

/// <summary>
/// Escucha ArchivosNuevos en UNC y procesa lotes TXT en paralelo mediante hilos
/// independientes (RF-01 a RF-05). La cantidad de procesos simultáneos es
/// parametrizable (FileSettings.MaxProcesosSimultaneos) y en ningún momento se
/// ejecutan dos archivos pertenecientes al mismo usuario al mismo tiempo.
/// Usa sondeo con impersonación; FileSystemWatcher no es fiable en rutas UNC como servicio Windows.
/// </summary>
public class LoteWatcherInfrastructure
{
    private readonly RedDisponibleService _redDisponible;
    private readonly ILoteProcesamientoService _loteProcesamiento;
    private readonly RegistroUsuariosEnProcesoService _registroUsuarios;
    private readonly ILogger<LoteWatcherInfrastructure> _logger;
    private readonly string _directorioArchivosNuevos;
    private readonly int _escaneoSegundos;
    private readonly int _maxProcesosSimultaneos;

    // Garantiza una asignación atómica de archivos entre hilos: dos hilos nunca
    // reservan el mismo TXT ni el mismo usuario al mismo tiempo.
    private readonly object _reservaLock = new();

    // Archivos que terminaron sin completarse (pendientes de revisión/reintento):
    // se evita reasignarlos de inmediato hasta cumplir el intervalo de escaneo.
    private readonly ConcurrentDictionary<string, DateTime> _enfriamientoArchivos =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationToken _stoppingToken;

    public LoteWatcherInfrastructure(
        IOptions<RutasSettings> rutasOptions,
        IOptions<FileSettings> fileSettings,
        RedDisponibleService redDisponible,
        ILoteProcesamientoService loteProcesamiento,
        RegistroUsuariosEnProcesoService registroUsuarios,
        ILogger<LoteWatcherInfrastructure> logger)
    {
        _redDisponible = redDisponible;
        _loteProcesamiento = loteProcesamiento;
        _registroUsuarios = registroUsuarios;
        _logger = logger;
        _directorioArchivosNuevos = rutasOptions.Value.RutaArchivosNuevos;
        _escaneoSegundos = Math.Max(1, fileSettings.Value.ArchivosNuevosEscaneoSegundos);
        _maxProcesosSimultaneos = Math.Max(1, fileSettings.Value.MaxProcesosSimultaneos);

        if (string.IsNullOrWhiteSpace(_directorioArchivosNuevos))
            throw new InvalidOperationException(
                "Modo Red requiere Rutas.RaizUnc configurado para resolver ArchivosNuevos.");
    }

    /// <summary>
    /// Lanza N hilos independientes (RF-03) que escuchan ArchivosNuevos de forma
    /// permanente (RF-01) y procesan lotes en paralelo respetando la exclusión por usuario.
    /// </summary>
    public async Task EjecutarEscuchaAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        _logger.LogInformation(
            "LoteWatcherIniciado | Ruta={Ruta} | EscaneoSegundos={EscaneoSegundos} | MaxProcesosSimultaneos={MaxProcesosSimultaneos}",
            _directorioArchivosNuevos,
            _escaneoSegundos,
            _maxProcesosSimultaneos);

        var hilos = Enumerable
            .Range(1, _maxProcesosSimultaneos)
            .Select(id => Task.Run(() => EjecutarHiloAsync(id, stoppingToken), stoppingToken))
            .ToArray();

        await Task.WhenAll(hilos);

        _logger.LogInformation("LoteWatcherDetenido | Ruta={Ruta}", _directorioArchivosNuevos);
    }

    private async Task EjecutarHiloAsync(int hiloId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!PuedeAccederArchivosNuevos())
                {
                    await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
                    continue;
                }

                var reserva = ReservarSiguienteLote();
                if (reserva is null)
                {
                    _logger.LogDebug(
                        "SinLoteAsignable | Hilo={Hilo} | Ruta={Ruta}",
                        hiloId,
                        _directorioArchivosNuevos);

                    await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
                    continue;
                }

                await ProcesarReservaAsync(hiloId, reserva);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en hilo de escucha de ArchivosNuevos | Hilo={Hilo}", hiloId);
                await Task.Delay(TimeSpan.FromSeconds(_escaneoSegundos), stoppingToken);
            }
        }
    }

    private async Task ProcesarReservaAsync(int hiloId, LoteReserva reserva)
    {
        var nombreTxt = Path.GetFileName(reserva.RutaTxt);

        try
        {
            _logger.LogInformation(
                "TxtAsignado | Hilo={Hilo} | Usuario={Usuario} | Archivo={Archivo}",
                hiloId,
                reserva.Usuario,
                nombreTxt);

            var continuarInmediato = await ProcesarTxtAsync(reserva.RutaTxt);

            _logger.LogInformation(
                "TxtProcesado | Hilo={Hilo} | Usuario={Usuario} | Archivo={Archivo} | RevisandoArchivosNuevos={RevisandoArchivosNuevos}",
                hiloId,
                reserva.Usuario,
                nombreTxt,
                continuarInmediato);

            if (continuarInmediato)
                _enfriamientoArchivos.TryRemove(reserva.RutaTxt, out _);
            else
                _enfriamientoArchivos[reserva.RutaTxt] =
                    DateTime.UtcNow.AddSeconds(_escaneoSegundos);
        }
        finally
        {
            // RF-05: liberar la tabla virtual del usuario al terminar su archivo.
            _registroUsuarios.Liberar(reserva.Usuario);
        }
    }

    /// <summary>
    /// Selecciona y reserva de forma atómica el siguiente TXT pendiente cuyo usuario
    /// no esté siendo procesado (RF-02, RF-05). Omite temporalmente los archivos de
    /// usuarios activos y continúa buscando otro archivo disponible.
    /// </summary>
    private LoteReserva? ReservarSiguienteLote()
    {
        lock (_reservaLock)
        {
            var pendientes = ListarTxtPendientes();
            if (pendientes.Count == 0)
                return null;

            var ahora = DateTime.UtcNow;

            foreach (var rutaTxt in pendientes)
            {
                if (_enfriamientoArchivos.TryGetValue(rutaTxt, out var disponibleEn) &&
                    ahora < disponibleEn)
                    continue;

                string usuario;
                try
                {
                    usuario = UsuarioArchivoResolver.Resolver(rutaTxt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "UsuarioNoResuelto | Archivo={Archivo}",
                        Path.GetFileName(rutaTxt));
                    continue;
                }

                if (_registroUsuarios.EstaActivo(usuario))
                {
                    _logger.LogDebug(
                        "UsuarioOcupadoOmitido | Usuario={Usuario} | Archivo={Archivo}",
                        usuario,
                        Path.GetFileName(rutaTxt));
                    continue;
                }

                if (_registroUsuarios.IntentarRegistrar(usuario))
                    return new LoteReserva(rutaTxt, usuario);
            }

            return null;
        }
    }

    private List<string> ListarTxtPendientes()
    {
        if (!_redDisponible.EstaDisponible())
            return [];

        return _redDisponible.EjecutarConAcceso(() =>
        {
            if (!Directory.Exists(_directorioArchivosNuevos))
                return new List<string>();

            return Directory.GetFiles(_directorioArchivosNuevos, "*.txt")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private bool PuedeAccederArchivosNuevos()
    {
        if (!_redDisponible.EstaDisponible())
        {
            _logger.LogWarning(
                "ArchivosNuevosNoAccesible | Ruta={Ruta} | Error={Error}",
                _directorioArchivosNuevos,
                _redDisponible.UltimoErrorMensaje);
            return false;
        }

        var carpetaExiste = _redDisponible.EjecutarConAcceso(() =>
            Directory.Exists(_directorioArchivosNuevos));

        if (carpetaExiste)
            return true;

        _logger.LogWarning(
            "ArchivosNuevosNoExiste | Ruta={Ruta} | Nota=La carpeta debe existir previamente en la UNC",
            _directorioArchivosNuevos);
        return false;
    }

    private async Task<bool> ProcesarTxtAsync(string rutaTxt)
    {
        try
        {
            await _redDisponible.EjecutarConAccesoAsync(async () =>
            {
                if (!File.Exists(rutaTxt))
                    return;

                await EsperarArchivoTxtDisponible(rutaTxt);
            });

            if (!_redDisponible.EjecutarConAcceso(() => File.Exists(rutaTxt)))
            {
                _logger.LogWarning(
                    "TxtNoDisponible | Archivo={Archivo}",
                    Path.GetFileName(rutaTxt));
                return false;
            }

            var resultado = await _loteProcesamiento.ProcesarLoteAsync(rutaTxt, _stoppingToken);

            return resultado.PermiteContinuarInmediato;
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // cierre del servicio
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoLote | Txt={Txt}",
                Path.GetFileName(rutaTxt));
            return false;
        }
    }

    private async Task EsperarArchivoTxtDisponible(string rutaTxt)
    {
        for (int i = 0; i < 20; i++)
        {
            _stoppingToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(
                    rutaTxt,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                if (stream.Length > 0)
                    return;
            }
            catch (IOException)
            {
                // archivo aún bloqueado
            }

            await Task.Delay(250, _stoppingToken);
        }
    }

    private sealed record LoteReserva(string RutaTxt, string Usuario);
}
