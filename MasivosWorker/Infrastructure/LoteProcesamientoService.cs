using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

namespace Infrastructure;

/// <summary>
/// Orquesta el ciclo completo de un lote (TXT) en UNC (RF-01 a RF-12).
/// </summary>
public class LoteProcesamientoService : ILoteProcesamientoService
{
    private readonly FileManagerInfraestructure _fileManager;
    private readonly IDocumentoProcesamientoService _documentoProcesamiento;
    private readonly IOpenAiBarcodeService _openAiBarcode;
    private readonly IEmailNotificationService _emailNotification;
    private readonly ITrazabilidadSqlService _trazabilidadSql;
    private readonly LogDiarioService _logDiario;
    private readonly RedDisponibleService _redDisponible;
    private readonly ILogger<LoteProcesamientoService> _logger;
    private readonly int _tamanoLote;
    private readonly int _maxArchivosConcurrentes;
    private readonly SemaphoreSlim _semaforo;

    public LoteProcesamientoService(
        FileManagerInfraestructure fileManager,
        IDocumentoProcesamientoService documentoProcesamiento,
        IOpenAiBarcodeService openAiBarcode,
        IEmailNotificationService emailNotification,
        ITrazabilidadSqlService trazabilidadSql,
        LogDiarioService logDiario,
        RedDisponibleService redDisponible,
        IOptions<FileSettings> fileSettings,
        ILogger<LoteProcesamientoService> logger)
    {
        _fileManager = fileManager;
        _documentoProcesamiento = documentoProcesamiento;
        _openAiBarcode = openAiBarcode;
        _emailNotification = emailNotification;
        _trazabilidadSql = trazabilidadSql;
        _logDiario = logDiario;
        _redDisponible = redDisponible;
        _logger = logger;
        _tamanoLote = Math.Max(1, fileSettings.Value.TamanoLote);
        _maxArchivosConcurrentes = Math.Max(1, fileSettings.Value.MaxArchivosConcurrentes);
        _semaforo = new SemaphoreSlim(_maxArchivosConcurrentes, _maxArchivosConcurrentes);
    }

    public Task<LoteProcesamientoOutcome> ProcesarLoteAsync(string rutaTxt, CancellationToken cancellationToken) =>
        _redDisponible.EjecutarConAccesoAsync(() => ProcesarLoteInternoAsync(rutaTxt, cancellationToken));

    private async Task<LoteProcesamientoOutcome> ProcesarLoteInternoAsync(string rutaTxt, CancellationToken cancellationToken)
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
            return LoteProcesamientoOutcome.PendienteRevision();
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
            return LoteProcesamientoOutcome.PendienteRevision();
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
            return LoteProcesamientoOutcome.PendienteRevision();
        }

        _logger.LogInformation(
            "LoteIniciado | Txt={Txt} | Usuario={Usuario} | Fecha={Fecha} | RutaProcesar={RutaProcesar} | TamanoLote={TamanoLote} | MaxArchivosConcurrentes={MaxArchivosConcurrentes}",
            nombreTxt,
            contexto.Usuario,
            contexto.Fecha,
            contexto.Procesar,
            _tamanoLote,
            _maxArchivosConcurrentes);

        var procesadosLote = 0;
        var noProcesadosLote = 0;
        string? errorOpenAiLote = null;
        var archivosAfectadosOpenAi = 0;
        var huboIncidenciaInfraestructura = false;

        // Intento 1: vaciar procesar por tandas
        while (true)
        {
            var tanda = TomarPdfs(contexto.Procesar, _tamanoLote);
            if (tanda.Count == 0)
                break;

            _logger.LogInformation("TandaIniciada | Cantidad={Cantidad}", tanda.Count);

            var resultadosTanda = await Task.WhenAll(tanda.Select(pdf =>
                ProcesarIntento1Async(pdf, contexto, cancellationToken)));

            foreach (var resultado in resultadosTanda)
            {
                procesadosLote += resultado.Procesados;
                noProcesadosLote += resultado.NoProcesados;
                huboIncidenciaInfraestructura |= resultado.HuboIncidenciaInfraestructura;
            }
        }

        // Intento 2: reprocesar error
        var archivosError = ListarPdfs(contexto.Error);
        var resultadosIntento2 = await Task.WhenAll(archivosError.Select(pdf =>
            ProcesarIntento2Async(pdf, contexto, cancellationToken)));

        foreach (var resultado in resultadosIntento2)
        {
            procesadosLote += resultado.Procesados;
            noProcesadosLote += resultado.NoProcesados;
            huboIncidenciaInfraestructura |= resultado.HuboIncidenciaInfraestructura;
        }

        // Intento 3: OpenAI sobre procesaria
        var archivosProcesaria = ListarPdfs(contexto.Procesaria);
        var resultadosIntento3 = await Task.WhenAll(archivosProcesaria.Select(pdf =>
            ProcesarIntento3OpenAiAsync(pdf, contexto, cancellationToken)));

        foreach (var resultado in resultadosIntento3)
        {
            switch (resultado.TipoResultado)
            {
                case Intento3ResultadoTipo.Exito:
                    procesadosLote++;
                    huboIncidenciaInfraestructura |= resultado.HuboIncidenciaInfraestructura;
                    break;
                case Intento3ResultadoTipo.NoProcesado:
                    noProcesadosLote++;
                    huboIncidenciaInfraestructura |= resultado.HuboIncidenciaInfraestructura;
                    break;
                case Intento3ResultadoTipo.ErrorOpenAi:
                    noProcesadosLote++;
                    errorOpenAiLote ??= resultado.ErrorMensaje;
                    archivosAfectadosOpenAi++;
                    huboIncidenciaInfraestructura |= resultado.HuboIncidenciaInfraestructura;
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(errorOpenAiLote))
        {
            await _emailNotification.EnviarFalloOpenAiLoteAsync(
                contexto,
                archivosAfectadosOpenAi,
                errorOpenAiLote,
                cancellationToken);
        }

        await _logDiario.IncrementarAsync(
            contexto,
            procesadosLote,
            noProcesadosLote,
            cancellationToken);

        if (huboIncidenciaInfraestructura)
        {
            _logger.LogWarning(
                "LotePendienteReintento | Txt={Txt} | Procesados={Procesados} | NoProcesados={NoProcesados}",
                nombreTxt,
                procesadosLote,
                noProcesadosLote);
            return LoteProcesamientoOutcome.PendienteReintento(procesadosLote, noProcesadosLote);
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
            return LoteProcesamientoOutcome.PendienteReintento(procesadosLote, noProcesadosLote);
        }

        return LoteProcesamientoOutcome.Completado(procesadosLote, noProcesadosLote);
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

    private async Task<IntentoDocumentoResultado> ProcesarIntento1Async(
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
            await RegistrarTrazabilidadAsync(contexto, rutaProcesando, resultado, cancellationToken);

            return AplicarResultadoIntento1(resultado, rutaProcesando, contexto);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Intento1ErrorInfraestructura | Archivo={Archivo}",
                nombre);

            return ManejarFalloInfraestructura(rutaPdf, contexto, ex, "Intento1");
        }
        finally
        {
            _semaforo.Release();
        }
    }

    private IntentoDocumentoResultado AplicarResultadoIntento1(
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
                return IntentoDocumentoResultado.Exito();

            case DocumentoProcesamientoEstado.FalloBarcode:
                _fileManager.MoverAProcesaria(rutaActual, contexto);
                _logger.LogWarning(
                    "Intento1Fallo | Archivo={Archivo} | Destino=procesaria | Estado={Estado}",
                    Path.GetFileName(rutaActual),
                    resultado.Estado);
                return IntentoDocumentoResultado.SinConteo();

            case DocumentoProcesamientoEstado.ErrorInesperado:
                _fileManager.MoverAError(rutaActual, contexto);
                _logger.LogWarning(
                    "Intento1Fallo | Archivo={Archivo} | Destino=error | Estado={Estado}",
                    Path.GetFileName(rutaActual),
                    resultado.Estado);
                return IntentoDocumentoResultado.SinConteo();

            case DocumentoProcesamientoEstado.FalloApiDatos:
            case DocumentoProcesamientoEstado.FalloApiFisico:
            case DocumentoProcesamientoEstado.PdfCorrupto:
                _fileManager.MoverANoprocesados(rutaActual, contexto);
                _logger.LogWarning(
                    "ArchivoMovido | Destino=noprocesados | Archivo={Archivo} | Estado={Estado}",
                    Path.GetFileName(rutaActual),
                    resultado.Estado);
                return IntentoDocumentoResultado.NoProcesado();

            default:
                _fileManager.MoverAError(rutaActual, contexto);
                return IntentoDocumentoResultado.SinConteo();
        }
    }

    private async Task<IntentoDocumentoResultado> ProcesarIntento2Async(
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
            await RegistrarTrazabilidadAsync(contexto, rutaPdf, resultado, cancellationToken);

            switch (resultado.Estado)
            {
                case DocumentoProcesamientoEstado.Exito:
                    _fileManager.MoverAProcesados(
                        rutaPdf,
                        resultado.Documento!.NombreArchivo,
                        contexto);
                    return IntentoDocumentoResultado.Exito();

                case DocumentoProcesamientoEstado.FalloApiDatos:
                case DocumentoProcesamientoEstado.FalloApiFisico:
                case DocumentoProcesamientoEstado.PdfCorrupto:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    return IntentoDocumentoResultado.NoProcesado();

                case DocumentoProcesamientoEstado.FalloBarcode:
                case DocumentoProcesamientoEstado.ErrorInesperado:
                    _fileManager.MoverAProcesaria(rutaPdf, contexto);
                    _logger.LogWarning(
                        "Intento2Fallo | Archivo={Archivo} | Destino=procesaria",
                        nombre);
                    return IntentoDocumentoResultado.SinConteo();

                default:
                    _fileManager.MoverAProcesaria(rutaPdf, contexto);
                    return IntentoDocumentoResultado.SinConteo();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Intento2ErrorInfraestructura | Archivo={Archivo}",
                nombre);

            return ManejarFalloInfraestructura(rutaPdf, contexto, ex, "Intento2");
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
            DocumentoProcesamientoResult resultado;

            switch (openAi.Tipo)
            {
                case OpenAiBarcodeResultKind.CodigoEncontrado:
                {
                    resultado = await _documentoProcesamiento.ProcesarConCodigoConocidoAsync(
                        rutaPdf,
                        openAi.Codigo!,
                        cancellationToken);

                    if (resultado.EsExitoso)
                    {
                        _fileManager.MoverAProcesados(
                            rutaPdf,
                            resultado.Documento!.NombreArchivo,
                            contexto);
                        await RegistrarTrazabilidadAsync(contexto, rutaPdf, resultado, cancellationToken);
                        return Intento3Resultado.Exito();
                    }

                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    await RegistrarTrazabilidadAsync(contexto, rutaPdf, resultado, cancellationToken);
                    return Intento3Resultado.NoProcesado();
                }

                case OpenAiBarcodeResultKind.NoBarcode:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    await RegistrarTrazabilidadAsync(contexto, rutaPdf, new DocumentoProcesamientoResult
                    {
                        Estado = DocumentoProcesamientoEstado.FalloBarcode
                    }, cancellationToken);
                    return Intento3Resultado.NoProcesado();

                case OpenAiBarcodeResultKind.ErrorServicio:
                default:
                    _fileManager.MoverANoprocesados(rutaPdf, contexto);
                    await RegistrarTrazabilidadAsync(contexto, rutaPdf, new DocumentoProcesamientoResult
                    {
                        Estado = DocumentoProcesamientoEstado.ErrorInesperado
                    }, cancellationToken);
                    return Intento3Resultado.ErrorOpenAi(openAi.ErrorMensaje ?? "Error OpenAI");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Intento3ErrorInfraestructura | Archivo={Archivo}",
                nombre);

            return ManejarFalloInfraestructuraIntento3(rutaPdf, contexto, ex);
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

    private async Task RegistrarTrazabilidadAsync(
        RutasLoteContext contexto,
        string rutaPdf,
        DocumentoProcesamientoResult resultado,
        CancellationToken cancellationToken)
    {
        var nombreArchivo = Path.GetFileName(rutaPdf);
        var procesado = resultado.Estado == DocumentoProcesamientoEstado.Exito;

        await _trazabilidadSql.RegistrarDocumentoAsync(
            contexto,
            nombreArchivo,
            resultado.Soporte,
            resultado.IdPaciente,
            resultado.IdBodega,
            resultado.IdCartera,
            resultado.FechaFactura,
            procesado,
            cancellationToken);
    }

    private IntentoDocumentoResultado ManejarFalloInfraestructura(
        string rutaPdf,
        RutasLoteContext contexto,
        Exception ex,
        string intento)
    {
        var movido = IntentarMoverAError(rutaPdf, contexto);

        _logger.LogWarning(
            ex,
            "{Intento}PendienteReintento | Archivo={Archivo} | MovidoAError={MovidoAError}",
            intento,
            Path.GetFileName(rutaPdf),
            movido);

        return IntentoDocumentoResultado.Infraestructura(noProcesados: movido ? 1 : 0);
    }

    private Intento3Resultado ManejarFalloInfraestructuraIntento3(
        string rutaPdf,
        RutasLoteContext contexto,
        Exception ex)
    {
        var movido = IntentarMoverANoprocesados(rutaPdf, contexto);

        _logger.LogWarning(
            ex,
            "Intento3PendienteReintento | Archivo={Archivo} | MovidoANoprocesados={MovidoANoprocesados}",
            Path.GetFileName(rutaPdf),
            movido);

        return Intento3Resultado.ErrorOpenAi(
            "Fallo de infraestructura durante el intento 3.",
            huboIncidenciaInfraestructura: true);
    }

    private bool IntentarMoverAError(string rutaPdf, RutasLoteContext contexto)
    {
        try
        {
            if (File.Exists(rutaPdf))
                _fileManager.MoverAError(rutaPdf, contexto);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ErrorMoviendoArchivoAError | Archivo={Archivo}", Path.GetFileName(rutaPdf));
            return false;
        }
    }

    private bool IntentarMoverANoprocesados(string rutaPdf, RutasLoteContext contexto)
    {
        try
        {
            if (File.Exists(rutaPdf))
                _fileManager.MoverANoprocesados(rutaPdf, contexto);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ErrorMoviendoArchivoANoprocesados | Archivo={Archivo}", Path.GetFileName(rutaPdf));
            return false;
        }
    }

    private sealed class IntentoDocumentoResultado
    {
        public int Procesados { get; init; }

        public int NoProcesados { get; init; }

        public bool HuboIncidenciaInfraestructura { get; init; }

        public static IntentoDocumentoResultado Exito() =>
            new() { Procesados = 1 };

        public static IntentoDocumentoResultado NoProcesado() =>
            new() { NoProcesados = 1 };

        public static IntentoDocumentoResultado SinConteo() =>
            new();

        public static IntentoDocumentoResultado Infraestructura(int noProcesados) =>
            new()
            {
                NoProcesados = noProcesados,
                HuboIncidenciaInfraestructura = true
            };
    }

    private sealed class Intento3Resultado
    {
        public Intento3ResultadoTipo TipoResultado { get; init; }

        public string? ErrorMensaje { get; init; }

        public bool HuboIncidenciaInfraestructura { get; init; }

        public static Intento3Resultado Exito() =>
            new() { TipoResultado = Intento3ResultadoTipo.Exito };

        public static Intento3Resultado NoProcesado() =>
            new() { TipoResultado = Intento3ResultadoTipo.NoProcesado };

        public static Intento3Resultado ErrorOpenAi(
            string mensaje,
            bool huboIncidenciaInfraestructura = false) =>
            new()
            {
                TipoResultado = Intento3ResultadoTipo.ErrorOpenAi,
                ErrorMensaje = mensaje,
                HuboIncidenciaInfraestructura = huboIncidenciaInfraestructura
            };
    }

    private enum Intento3ResultadoTipo
    {
        Exito,
        NoProcesado,
        ErrorOpenAi
    }
}
