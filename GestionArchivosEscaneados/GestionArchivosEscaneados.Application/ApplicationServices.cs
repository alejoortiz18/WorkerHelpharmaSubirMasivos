using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Infrastructure.Barcode;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Enums;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Application;

public class AuthAppService
{
    private readonly UsuarioAuthService _usuarioAuth;

    public AuthAppService(UsuarioAuthService usuarioAuth)
    {
        _usuarioAuth = usuarioAuth;
    }

    public Task<ValidacionLoginResult> ValidarLoginAsync(
        string usuarioIngresado,
        CancellationToken cancellationToken = default) =>
        _usuarioAuth.ValidarLoginAsync(usuarioIngresado, cancellationToken);
}

public class CalendarioAppService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;

    public CalendarioAppService(ITrazabilidadConsultaSqlService trazabilidad)
    {
        _trazabilidad = trazabilidad;
    }

    public Task<IReadOnlyList<string>> ObtenerFechasDisponiblesAsync(
        string usuario,
        CancellationToken cancellationToken = default) =>
        _trazabilidad.ListarFechasDisponiblesAsync(usuario, cancellationToken);

    public Task<bool> FechaExisteAsync(
        string usuario,
        string fecha,
        CancellationToken cancellationToken = default) =>
        _trazabilidad.FechaExisteAsync(usuario, fecha, cancellationToken);
}

public class DashboardAppService
{
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;

    public DashboardAppService(ITrazabilidadConsultaSqlService trazabilidad)
    {
        _trazabilidad = trazabilidad;
    }

    public async Task<ResumenLogDiario?> ObtenerResumenAsync(
        string usuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        var existe = await _trazabilidad.FechaExisteAsync(usuario, fecha, cancellationToken);
        if (!existe)
            return null;

        return await _trazabilidad.ObtenerResumenAsync(usuario, fecha, cancellationToken);
    }
}

public class ReprocesoAppService
{
    private readonly UncStorageService _unc;
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;
    private readonly IBarcodeRegionService _barcodeRegionService;
    private readonly IOpenAiBarcodeService _openAiBarcodeService;
    private readonly Infrastructure.Api.ISoporteProcesamientoService _soporte;
    private readonly IOptions<FileSettings> _fileSettings;
    private readonly ILogger<ReprocesoAppService> _logger;

    public ReprocesoAppService(
        UncStorageService unc,
        ITrazabilidadConsultaSqlService trazabilidad,
        IBarcodeRegionService barcodeRegionService,
        IOpenAiBarcodeService openAiBarcodeService,
        Infrastructure.Api.ISoporteProcesamientoService soporte,
        IOptions<FileSettings> fileSettings,
        ILogger<ReprocesoAppService> logger)
    {
        _unc = unc;
        _trazabilidad = trazabilidad;
        _barcodeRegionService = barcodeRegionService;
        _openAiBarcodeService = openAiBarcodeService;
        _soporte = soporte;
        _fileSettings = fileSettings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ArchivoNoProcesado>> ListarNoProcesadosAsync(
        string usuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        var pendientes = await _trazabilidad.ListarDocumentosPendientesAsync(usuario, fecha, cancellationToken);
        var pendientesPorArchivo = pendientes.ToDictionary(
            documento => documento.NombreArchivo,
            documento => documento,
            StringComparer.OrdinalIgnoreCase);

        var archivosUnc = _unc.ListarNoProcesados(usuario, fecha);
        var visibles = archivosUnc
            .Where(archivo => pendientesPorArchivo.ContainsKey(archivo.NombreArchivo))
            .Select(archivo =>
            {
                pendientesPorArchivo.TryGetValue(archivo.NombreArchivo, out var pendiente);

                return new ArchivoNoProcesado
                {
                    NombreArchivo = archivo.NombreArchivo,
                    Fecha = archivo.Fecha,
                    RutaCompleta = archivo.RutaCompleta,
                    TieneIntentoPrevio = archivo.TieneIntentoPrevio || pendiente?.TieneIntentoPrevio == true
                };
            }).ToList();

        var faltantes = pendientes
            .Where(documento => !archivosUnc.Any(archivo =>
                string.Equals(archivo.NombreArchivo, documento.NombreArchivo, StringComparison.OrdinalIgnoreCase)))
            .Select(documento => documento.NombreArchivo)
            .ToList();

        if (faltantes.Count > 0)
        {
            _logger.LogWarning(
                "ReprocesoPendientesSinPdf | Usuario={Usuario} | Fecha={Fecha} | Cantidad={Cantidad} | Archivos={Archivos}",
                usuario,
                fecha,
                faltantes.Count,
                string.Join(", ", faltantes));
        }

        return visibles;
    }

    public byte[]? LeerPdfNoProcesado(string usuario, string fecha, string nombreArchivo) =>
        _unc.LeerPdfNoProcesado(usuario, fecha, nombreArchivo);

    public async Task<SoporteProcesamientoEstado> ProcesarConCodigoConocidoAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        string codigoBarras,
        CancellationToken cancellationToken = default)
    {
        if (!await _trazabilidad.DocumentoPendienteExisteAsync(usuario, fecha, nombreArchivo, cancellationToken))
        {
            _logger.LogWarning(
                "ProcesoDocumentoSinRegistroBd | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
                usuario,
                fecha,
                nombreArchivo);
            return SoporteProcesamientoEstado.FalloApiDatos;
        }

        var pdf = _unc.LeerPdfNoProcesado(usuario, fecha, nombreArchivo);
        if (pdf == null)
            return SoporteProcesamientoEstado.FalloApiDatos;

        var resultado = await _soporte.ProcesarAsync(
            codigoBarras.Trim(),
            pdf,
            nombreArchivo,
            usuario,
            cancellationToken);

        if (!resultado.EsExitoso)
            return resultado.Estado;

        var actualizado = await _trazabilidad.MarcarDocumentoProcesadoAsync(
            usuario,
            fecha,
            nombreArchivo,
            resultado.Soporte,
            resultado.Datos?.IdPaciente,
            cancellationToken);
        if (!actualizado)
            return SoporteProcesamientoEstado.ErrorInesperado;

        var rutas = _unc.ObtenerRutasDia(usuario, fecha);
        var ruta = Path.Combine(rutas.Noprocesados, nombreArchivo);
        _unc.MoverANoprocesadosAProcesados(ruta, rutas);

        return SoporteProcesamientoEstado.Exito;
    }

    public async Task<SoporteProcesamientoEstado> ReprocesarAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        string codigoBarras,
        CancellationToken cancellationToken = default)
    {
        _ = codigoBarras;
        _logger.LogInformation(
            "ReprocesoInicio | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
            usuario,
            fecha,
            nombreArchivo);

        if (!await _trazabilidad.DocumentoPendienteExisteAsync(usuario, fecha, nombreArchivo, cancellationToken))
        {
            _logger.LogWarning(
                "ReprocesoDocumentoSinRegistroBd | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
                usuario,
                fecha,
                nombreArchivo);
            return SoporteProcesamientoEstado.FalloApiDatos;
        }

        var rutaPdf = _unc.ResolverRutaPdfSegura(usuario, fecha, nombreArchivo);
        if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
        {
            _logger.LogWarning(
                "ReprocesoPdfNoEncontrado | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
                usuario,
                fecha,
                nombreArchivo);
            return SoporteProcesamientoEstado.FalloApiDatos;
        }

        var codigoDetectado = await LeerCodigoBarrasAsync(rutaPdf, cancellationToken);
        if (string.IsNullOrWhiteSpace(codigoDetectado))
        {
            _logger.LogInformation(
                "ReprocesoBarcodeNoDetectado | Archivo={Archivo} | Accion=EnviarOpenAI",
                nombreArchivo);

            var resultadoOpenAi = await _openAiBarcodeService.LeerCodigoAsync(rutaPdf, cancellationToken);
            _logger.LogInformation(
                "ReprocesoOpenAiResultado | Archivo={Archivo} | Tipo={Tipo} | Codigo={Codigo}",
                nombreArchivo,
                resultadoOpenAi.Tipo,
                resultadoOpenAi.Codigo ?? "-");

            switch (resultadoOpenAi.Tipo)
            {
                case OpenAiBarcodeResultKind.CodigoEncontrado:
                    codigoDetectado = resultadoOpenAi.Codigo;
                    break;
                case OpenAiBarcodeResultKind.NoBarcode:
                    return SoporteProcesamientoEstado.FalloBarcode;
                case OpenAiBarcodeResultKind.ErrorServicio:
                default:
                    return SoporteProcesamientoEstado.FalloOpenAi;
            }
        }
        else
        {
            _logger.LogInformation(
                "ReprocesoBarcodeDetectado | Archivo={Archivo} | Codigo={Codigo}",
                nombreArchivo,
                codigoDetectado);
        }

        if (string.IsNullOrWhiteSpace(codigoDetectado))
            return SoporteProcesamientoEstado.FalloBarcode;

        var pdf = await File.ReadAllBytesAsync(rutaPdf, cancellationToken);
        _logger.LogInformation(
            "ReprocesoEnviarSoporte | Archivo={Archivo} | Codigo={Codigo} | Bytes={Bytes}",
            nombreArchivo,
            codigoDetectado,
            pdf.Length);

        var resultado = await _soporte.ProcesarAsync(
            codigoDetectado,
            pdf,
            nombreArchivo,
            usuario,
            cancellationToken);

        if (!resultado.EsExitoso)
        {
            _logger.LogWarning(
                "ReprocesoSoporteFallo | Archivo={Archivo} | Codigo={Codigo} | Estado={Estado}",
                nombreArchivo,
                codigoDetectado,
                resultado.Estado);
            return resultado.Estado;
        }

        var actualizado = await _trazabilidad.MarcarDocumentoProcesadoAsync(
            usuario,
            fecha,
            nombreArchivo,
            resultado.Soporte,
            resultado.Datos?.IdPaciente,
            cancellationToken);
        if (!actualizado)
            return SoporteProcesamientoEstado.ErrorInesperado;

        var rutas = _unc.ObtenerRutasDia(usuario, fecha);
        var ruta = Path.Combine(rutas.Noprocesados, nombreArchivo);
        _unc.MoverANoprocesadosAProcesados(ruta, rutas);
        _logger.LogInformation(
            "ReprocesoExitoso | Archivo={Archivo} | Codigo={Codigo}",
            nombreArchivo,
            codigoDetectado);

        return SoporteProcesamientoEstado.Exito;
    }

    public async Task<ReprocesoLoteItemResult> ProcesarItemAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        string codigoBarras,
        CancellationToken cancellationToken = default)
    {
        var estado = await ProcesarConCodigoConocidoAsync(usuario, fecha, nombreArchivo, codigoBarras, cancellationToken);
        return new ReprocesoLoteItemResult
        {
            NombreArchivo = nombreArchivo,
            CodigoBarras = codigoBarras.Trim(),
            Exito = estado == SoporteProcesamientoEstado.Exito,
            Estado = estado
        };
    }

    public async Task<bool> EliminarAsync(
        string usuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        _unc.EliminarPdfNoProcesado(usuario, fecha, nombreArchivo);

        var eliminadoSql = await _trazabilidad.EliminarDocumentoPendienteAsync(
            usuario,
            fecha,
            nombreArchivo,
            cancellationToken);

        return eliminadoSql;
    }

    private async Task<string?> LeerCodigoBarrasAsync(
        string rutaPdf,
        CancellationToken cancellationToken)
    {
        var settings = _fileSettings.Value;
        var maxReintentos = Math.Max(1, settings.BarcodeMaxReintentos);
        var esperaMs = Math.Max(100, settings.BarcodeEsperaMs);

        for (var intento = 1; intento <= maxReintentos; intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var codigo = await Task.Run(
                    () => _barcodeRegionService.LeerCodigoDesdePdf(rutaPdf),
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(codigo))
                    return codigo;
            }
            catch (Exception)
            {
                // Mantiene el mismo principio del worker: reintentar antes de rendirse.
                if (intento >= maxReintentos)
                    return null;
            }

            if (intento < maxReintentos)
            {
                await Task.Delay(esperaMs, cancellationToken);
            }
        }

        return null;
    }
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddGestionArchivosApplication(this IServiceCollection services)
    {
        services.AddSingleton<AuthAppService>();
        services.AddSingleton<CalendarioAppService>();
        services.AddSingleton<DashboardAppService>();
        services.AddSingleton<ReprocesoAppService>();
        return services;
    }
}
