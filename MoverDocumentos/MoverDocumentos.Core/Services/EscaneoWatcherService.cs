using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

public class EscaneoWatcherService : BackgroundService
{
    private readonly RutasSettings _rutas;
    private readonly ArchivoSettings _archivo;
    private readonly ReintentosSettings _reintentos;
    private readonly ILogger<EscaneoWatcherService> _logger;
    private readonly UsuarioService _usuarioService;
    private readonly RegistroUsuarioService _registroUsuarioService;
    private readonly EstructuraCarpetasService _estructuraCarpetasService;
    private readonly RedDisponibleService _redDisponibleService;
    private readonly MoverArchivoService _moverArchivoService;
    private readonly LoteService _loteService;

    private readonly SemaphoreSlim _semaforoGlobal = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _turnosPorArchivo =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private string _carpetaLocalNormalizada = string.Empty;
    private CancellationToken _stoppingToken;

    public EscaneoWatcherService(
        IOptions<RutasSettings> rutasOptions,
        IOptions<ArchivoSettings> archivoOptions,
        IOptions<ReintentosSettings> reintentosOptions,
        ILogger<EscaneoWatcherService> logger,
        UsuarioService usuarioService,
        RegistroUsuarioService registroUsuarioService,
        EstructuraCarpetasService estructuraCarpetasService,
        RedDisponibleService redDisponibleService,
        MoverArchivoService moverArchivoService,
        LoteService loteService)
    {
        _rutas = rutasOptions.Value;
        _archivo = archivoOptions.Value;
        _reintentos = reintentosOptions.Value;
        _logger = logger;
        _usuarioService = usuarioService;
        _registroUsuarioService = registroUsuarioService;
        _estructuraCarpetasService = estructuraCarpetasService;
        _redDisponibleService = redDisponibleService;
        _moverArchivoService = moverArchivoService;
        _loteService = loteService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        Directory.CreateDirectory(_rutas.CarpetaLocal);
        _carpetaLocalNormalizada = NormalizarDirectorio(_rutas.CarpetaLocal);

        IniciarWatcher();

        await EscanearCarpetaLocalAsync(stoppingToken);

        var tareaRespaldo = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_archivo.EscaneoRespaldoSegundos),
                        stoppingToken);
                    await EscanearCarpetaLocalAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error en escaneo periódico de carpeta local");
                }
            }
        }, stoppingToken);

        var tareaReintentos = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_reintentos.IntervaloSegundosRed),
                        stoppingToken);
                    await ReintentarPendientesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, stoppingToken);

        await Task.WhenAll(tareaRespaldo, tareaReintentos);
    }

    private void IniciarWatcher()
    {
        _watcher = new FileSystemWatcher(_rutas.CarpetaLocal)
        {
            Filter = "*.pdf",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                            NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536
        };

        _watcher.Created += (_, e) => EncolarArchivo(e.FullPath);
        _watcher.Changed += (_, e) => EncolarArchivo(e.FullPath);
        _watcher.Renamed += (_, e) => EncolarArchivo(e.FullPath);
        _watcher.Error += (_, _) =>
        {
            _logger.LogError(
                "FileSystemWatcherError | El escaneo periódico seguirá detectando archivos en la carpeta local.");
        };

        _logger.LogInformation(
            "WatcherIniciado | Ruta={Ruta}",
            _carpetaLocalNormalizada);
    }

    private void EncolarArchivo(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return;

        _ = Task.Run(() => ProcesarArchivoAsync(ruta, _stoppingToken));
    }

    private async Task EscanearCarpetaLocalAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_rutas.CarpetaLocal))
            return;

        var archivos = Directory.GetFiles(_rutas.CarpetaLocal, "*.pdf");
        if (archivos.Length == 0)
            return;

        _logger.LogDebug(
            "EscaneoLocal | ArchivosEncontrados={Cantidad}",
            archivos.Length);

        foreach (var archivo in archivos)
            _ = Task.Run(() => ProcesarArchivoAsync(archivo, stoppingToken), stoppingToken);
    }

    private async Task ReintentarPendientesAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_rutas.CarpetaLocal))
            return;

        var pendientes = Directory.GetFiles(_rutas.CarpetaLocal, "*.pdf");
        if (pendientes.Length == 0)
            return;

        if (!_redDisponibleService.EstaDisponible())
            return;

        _logger.LogInformation(
            "ReintentoPendientes | Cantidad={Cantidad}",
            pendientes.Length);

        foreach (var archivo in pendientes)
            await ProcesarArchivoAsync(archivo, stoppingToken);
    }

    private async Task ProcesarArchivoAsync(string ruta, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(ruta) ||
            !ruta.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            ruta = Path.GetFullPath(ruta);
        }
        catch
        {
            return;
        }

        if (!File.Exists(ruta))
            return;

        if (!EsRutaEnCarpetaLocal(ruta))
            return;

        var turno = _turnosPorArchivo.GetOrAdd(ruta, _ => new SemaphoreSlim(1, 1));
        await turno.WaitAsync(stoppingToken);

        try
        {
            if (!File.Exists(ruta))
                return;

            await _semaforoGlobal.WaitAsync(stoppingToken);

            try
            {
                if (!_redDisponibleService.EstaDisponible())
                {
                    _logger.LogError(
                        "RedNoDisponible | RaizUnc={RaizUnc}",
                        _rutas.RaizUnc);
                    return;
                }

                await EsperarArchivoDisponibleAsync(ruta, stoppingToken);

                var usuario = _usuarioService.ObtenerUsuarioNormalizado();
                var fecha = DateOnly.FromDateTime(DateTime.Now);

                _registroUsuarioService.RegistrarSiNoExiste(usuario);

                var carpetaProcesar = _estructuraCarpetasService.CrearEstructuraDia(usuario, fecha);
                _moverArchivoService.Mover(ruta, carpetaProcesar);
                _loteService.RegistrarMovimiento(usuario, fecha, carpetaProcesar);
            }
            finally
            {
                _semaforoGlobal.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoArchivo | Archivo={Archivo} | Mensaje={Mensaje}",
                Path.GetFileName(ruta),
                ex.Message);
        }
        finally
        {
            turno.Release();
        }
    }

    private async Task EsperarArchivoDisponibleAsync(string ruta, CancellationToken stoppingToken)
    {
        long ultimaLongitud = -1;
        var lecturasEstables = 0;
        Exception? ultimoError = null;
        var intentos = Math.Max(1, _archivo.EsperaIntentos);
        var esperaMs = Math.Max(100, _archivo.EsperaMs);
        var lecturasRequeridas = Math.Max(1, _archivo.LecturasEstables);

        for (var i = 0; i < intentos; i++)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(ruta))
                {
                    ultimoError = new FileNotFoundException("El archivo no existe en disco.", ruta);
                    lecturasEstables = 0;
                    ultimaLongitud = -1;
                }
                else
                {
                    using var stream = new FileStream(
                        ruta,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);

                    var longitud = stream.Length;
                    if (longitud <= 0)
                    {
                        ultimoError = new InvalidDataException("El archivo tiene tamaño cero.");
                        lecturasEstables = 0;
                        ultimaLongitud = -1;
                    }
                    else if (longitud == ultimaLongitud)
                    {
                        lecturasEstables++;
                        if (lecturasEstables >= lecturasRequeridas)
                            return;
                    }
                    else
                    {
                        ultimaLongitud = longitud;
                        lecturasEstables = 1;
                        ultimoError = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ultimoError = ex;
                lecturasEstables = 0;
                ultimaLongitud = -1;
            }

            await Task.Delay(esperaMs, stoppingToken);
        }

        var detalle = ultimoError?.Message ?? "tiempo de espera agotado";
        throw new IOException(
            $"El archivo no está disponible tras {intentos} intentos: {detalle}",
            ultimoError);
    }

    private bool EsRutaEnCarpetaLocal(string rutaCompleta)
    {
        var directorio = NormalizarDirectorio(Path.GetDirectoryName(rutaCompleta)!);
        return string.Equals(directorio, _carpetaLocalNormalizada, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizarDirectorio(string ruta) =>
        Path.GetFullPath(ruta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public override void Dispose()
    {
        _watcher?.Dispose();
        _semaforoGlobal.Dispose();
        base.Dispose();
    }
}
