using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

namespace Infrastructure;

/// <summary>
/// Orquesta el ciclo completo de un lote (TXT) en UNC (RF-01 a RF-12).
/// </summary>
public class LoteProcesamientoService
{
    private readonly FileManagerInfraestructure _fileManager;
    private readonly IDocumentoProcesamientoService _documentoProcesamiento;
    private readonly IOpenAiBarcodeService _openAiBarcode;
    private readonly IEmailNotificationService _emailNotification;
    private readonly LogDiarioService _logDiario;
    private readonly ILogger<LoteProcesamientoService> _logger;
    private readonly int _tamanoLote;
    private readonly SemaphoreSlim _semaforo;

    public LoteProcesamientoService(
        FileManagerInfraestructure fileManager,
        IDocumentoProcesamientoService documentoProcesamiento,
        IOpenAiBarcodeService openAiBarcode,
        IEmailNotificationService emailNotification,
        LogDiarioService logDiario,
        IOptions<FileSettings> fileSettings,
        ILogger<LoteProcesamientoService> logger)
    {
        _fileManager = fileManager;
        _documentoProcesamiento = documentoProcesamiento;
        _openAiBarcode = openAiBarcode;
        _emailNotification = emailNotification;
        _logDiario = logDiario;
        _logger = logger;
        _tamanoLote = Math.Max(1, fileSettings.Value.TamanoLote);
        _semaforo = new SemaphoreSlim(fileSettings.Value.MaxArchivosConcurrentes);
    }

    public async Task ProcesarLoteAsync(string rutaTxt, CancellationToken cancellationToken)
    {
        var nombreTxt = Path.GetFileName(rutaTxt);
        _logger.LogInformation("LoteDetectado | Txt={Txt}", nombreTxt);

        string rutaProcesar;
        try
        {
            rutaProcesar = await LeerRutaProcesarDesdeTxtAsync(rutaTxt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "LoteInvalido | Txt={Txt} | Motivo={Motivo}",
                nombreTxt,
                ex.Message);
            return;
        }

        RutasLoteContext contexto;
        try
        {
            contexto = RutasLoteResolver.Resolver(rutaProcesar);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "LoteRutasInvalidas | Txt={Txt} | RutaProcesar={RutaProcesar}",
                nombreTxt,
                rutaProcesar);
            return;
        }

        try
        {
            _fileManager.ValidarCarpetasLoteExisten(contexto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "LoteCarpetasIncompletas | Txt={Txt} | Motivo={Motivo}",
                nombreTxt,
                ex.Message);
            return;
        }

        _logger.LogInformation(
            "LoteIniciado | Txt={Txt} | Usuario={Usuario} | Fecha={Fecha} | RutaProcesar={RutaProcesar}",
            nombreTxt,
            contexto.Usuario,
            contexto.Fecha,
            contexto.Procesar);

        var procesadosLote = 0;
        var noProcesadosLote = 0;
        string? errorOpenAiLote = null;
        var archivosAfectadosOpenAi = 0;

        // Intento 1: vaciar procesar por tandas
        while (true)
        {
            var tanda = TomarPdfs(contexto.Procesar, _tamanoLote);
            if (tanda.Count == 0)
                break;

            _logger.LogInformation("TandaIniciada | Cantidad={Cantidad}", tanda.Count);

            foreach (var pdf in tanda)
            {
                var (procesados, noProcesados) = await ProcesarIntento1Async(
                    pdf,
                    contexto,
                    cancellationToken);

                procesadosLote += procesados;
                noProcesadosLote += noProcesados;
            }
        }

        // Intento 2: reprocesar error
        var archivosError = ListarPdfs(contexto.Error);
        foreach (var pdf in archivosError.ToList())
        {
            var (procesados, noProcesados) = await ProcesarIntento2Async(
                pdf,
                contexto,
                cancellationToken);

            procesadosLote += procesados;
            noProcesadosLote += noProcesados;
        }

        // Intento 3: OpenAI sobre procesaria
        var archivosProcesaria = ListarPdfs(contexto.Procesaria);
        foreach (var pdf in archivosProcesaria.ToList())
        {
            var resultado = await ProcesarIntento3OpenAiAsync(pdf, contexto, cancellationToken);

            switch (resultado.TipoResultado)
            {
                case Intento3ResultadoTipo.Exito:
                    procesadosLote++;
                    break;
                case Intento3ResultadoTipo.NoProcesado:
                    noProcesadosLote++;
                    break;
                case Intento3ResultadoTipo.ErrorOpenAi:
                    noProcesadosLote++;
                    errorOpenAiLote ??= resultado.ErrorMensaje;
                    archivosAfectadosOpenAi++;
                    break;
            }
        }

        await _logDiario.IncrementarAsync(contexto, procesadosLote, noProcesadosLote, cancellationToken);

        if (!string.IsNullOrWhiteSpace(errorOpenAiLote))
        {
            await _emailNotification.EnviarFalloOpenAiLoteAsync(
                contexto,
                archivosAfectadosOpenAi,
                errorOpenAiLote,
                cancellationToken);
        }

        _fileManager.LimpiarArchivosTemporales(contexto);

        try
        {
            if (File.Exists(rutaTxt))
            {
                File.Delete(rutaTxt);
                _logger.LogInformation("LoteFinalizado | TxtEliminado={Txt}", nombreTxt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ErrorEliminandoTxt | Txt={Txt}", nombreTxt);
        }
    }

    public static async Task<string> LeerRutaProcesarDesdeTxtAsync(
        string rutaTxt,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaTxt))
            throw new FileNotFoundException($"TXT de lote no encontrado: {rutaTxt}");

        var lineas = await File.ReadAllLinesAsync(rutaTxt, cancellationToken);
        if (lineas.Length == 0 || string.IsNullOrWhiteSpace(lineas[0]))
            throw new InvalidOperationException($"TXT de lote vacío o sin ruta en línea 1: {rutaTxt}");

        var rutaProcesar = lineas[0].Trim();

        if (!Directory.Exists(rutaProcesar))
            throw new InvalidOperationException($"Carpeta procesar no existe: {rutaProcesar}");

        return rutaProcesar;
    }

    private async Task<(int Procesados, int NoProcesados)> ProcesarIntento1Async(
        string rutaPdf,
        RutasLoteContext contexto,
        CancellationToken cancellationToken)
    {
        var nombre = Path.GetFileName(rutaPdf);

        _logger.LogInformation(
            "ProcesamientoIniciado | Archivo={Archivo} | Intento=1",
            nombre);

        await _semaforo.WaitAsync(cancellationToken);

        try
        {
            var rutaProcesando = _fileManager.MoverAProcesando(rutaPdf, contexto);
            var resultado = await _documentoProcesamiento.ProcesarAsync(rutaProcesando, cancellationToken);

            return AplicarResultadoIntento1(resultado, rutaProcesando, contexto);
        }
        finally
        {
            _semaforo.Release();
        }
    }

    private (int Procesados, int NoProcesados) AplicarResultadoIntento1(
        DocumentoProcesamientoResult resultado,
        string rutaActual,
        RutasLoteContext contexto)
    {
        switch (resultado.Estado)
        {
            case DocumentoProcesamientoEstado.Exito:
                _fileManager.MoverAProcesados(
                    rutaActual,
                    resultado.Documento!.NombreArchivo,
                    contexto);
                return (1, 0);

            case DocumentoProcesamientoEstado.FalloBarcode:
            case DocumentoProcesamientoEstado.ErrorInesperado:
                _fileManager.MoverAError(rutaActual, contexto);
                _logger.LogWarning(
                    "Intento1Fallo | Archivo={Archivo} | Destino=error | Estado={Estado}",
                    Path.GetFileName(rutaActual),
                    resultado.Estado);
                return (0, 0);

            case DocumentoProcesamientoEstado.FalloApiDatos:
            case DocumentoProcesamientoEstado.FalloApiFisico:
            case DocumentoProcesamientoEstado.PdfCorrupto:
                _fileManager.MoverANoprocesados(rutaActual, contexto);
                _logger.LogWarning(
                    "ArchivoMovido | Destino=noprocesados | Archivo={Archivo} | Estado={Estado}",
                    Path.GetFileName(rutaActual),
                    resultado.Estado);
                return (0, 1);

            default:
                _fileManager.MoverAError(rutaActual, contexto);
                return (0, 0);
        }
    }

    private async Task<(int Procesados, int NoProcesados)> ProcesarIntento2Async(
        string rutaPdf,
        RutasLoteContext contexto,
        CancellationToken cancellationToken)
    {
        var nombre = Path.GetFileName(rutaPdf);

        _logger.LogInformation(
            "ProcesamientoIniciado | Archivo={Archivo} | Intento=2",
            nombre);

        await _semaforo.WaitAsync(cancellationToken);

        try
        {
            var resultado = await _documentoProcesamiento.ProcesarAsync(rutaPdf, cancellationToken);

            switch (resultado.Estado)
            {
                case DocumentoProcesamientoEstado.Exito:
                    _fileManager.MoverAProcesados(
                        rutaPdf,
                        resultado.Documento!.NombreArchivo,
                        contexto);
                    return (1, 0);

                case DocumentoProcesamientoEstado.FalloApiDatos:
                case DocumentoProcesamientoEstado.FalloApiFisico:
                case DocumentoProcesamientoEstado.PdfCorrupto:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    return (0, 1);

                case DocumentoProcesamientoEstado.FalloBarcode:
                case DocumentoProcesamientoEstado.ErrorInesperado:
                    _fileManager.MoverAProcesaria(rutaPdf, contexto);
                    _logger.LogWarning(
                        "Intento2Fallo | Archivo={Archivo} | Destino=procesaria",
                        nombre);
                    return (0, 0);

                default:
                    _fileManager.MoverAProcesaria(rutaPdf, contexto);
                    return (0, 0);
            }
        }
        finally
        {
            _semaforo.Release();
        }
    }

    private async Task<Intento3Resultado> ProcesarIntento3OpenAiAsync(
        string rutaPdf,
        RutasLoteContext contexto,
        CancellationToken cancellationToken)
    {
        var nombre = Path.GetFileName(rutaPdf);

        _logger.LogInformation(
            "ProcesamientoIniciado | Archivo={Archivo} | Intento=3",
            nombre);

        await _semaforo.WaitAsync(cancellationToken);

        try
        {
            var openAi = await _openAiBarcode.LeerCodigoAsync(rutaPdf, cancellationToken);

            switch (openAi.Tipo)
            {
                case OpenAiBarcodeResultKind.CodigoEncontrado:
                {
                    var resultado = await _documentoProcesamiento.ProcesarConCodigoConocidoAsync(
                        rutaPdf,
                        openAi.Documento!,
                        cancellationToken);

                    if (resultado.EsExitoso)
                    {
                        _fileManager.MoverAProcesados(
                            rutaPdf,
                            resultado.Documento!.NombreArchivo,
                            contexto);
                        return Intento3Resultado.Exito();
                    }

                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    return Intento3Resultado.NoProcesado();
                }

                case OpenAiBarcodeResultKind.NoBarcode:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    return Intento3Resultado.NoProcesado();

                case OpenAiBarcodeResultKind.ErrorServicio:
                default:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    return Intento3Resultado.ErrorOpenAi(openAi.ErrorMensaje ?? "Error OpenAI");
            }
        }
        finally
        {
            _semaforo.Release();
        }
    }

    private static List<string> TomarPdfs(string carpeta, int cantidad)
    {
        if (!Directory.Exists(carpeta))
            return [];

        return Directory.GetFiles(carpeta, "*.pdf")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Take(cantidad)
            .ToList();
    }

    private static IReadOnlyList<string> ListarPdfs(string carpeta)
    {
        if (!Directory.Exists(carpeta))
            return [];

        return Directory.GetFiles(carpeta, "*.pdf")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class Intento3Resultado
    {
        public Intento3ResultadoTipo TipoResultado { get; init; }

        public string? ErrorMensaje { get; init; }

        public static Intento3Resultado Exito() =>
            new() { TipoResultado = Intento3ResultadoTipo.Exito };

        public static Intento3Resultado NoProcesado() =>
            new() { TipoResultado = Intento3ResultadoTipo.NoProcesado };

        public static Intento3Resultado ErrorOpenAi(string mensaje) =>
            new() { TipoResultado = Intento3ResultadoTipo.ErrorOpenAi, ErrorMensaje = mensaje };
    }

    private enum Intento3ResultadoTipo
    {
        Exito,
        NoProcesado,
        ErrorOpenAi
    }
}
