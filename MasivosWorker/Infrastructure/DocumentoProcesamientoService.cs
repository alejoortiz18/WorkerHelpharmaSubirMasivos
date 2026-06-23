using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

namespace Infrastructure;

/// <summary>
/// Lectura de barcode + integración APIs para un PDF ya ubicado en disco.
/// Orquesta lectura de barcode + integración APIs para un PDF en un lote UNC.
/// </summary>
public class DocumentoProcesamientoService : IDocumentoProcesamientoService
{
    private readonly BarcodeRegionService _barcodeRegionService;
    private readonly ISoporteProcesamientoService _soporteProcesamiento;
    private readonly ILogger<DocumentoProcesamientoService> _logger;
    private readonly int _maxReintentos;
    private readonly int _esperaMs;
    private readonly int _archivoEsperaIntentos;
    private readonly int _archivoEsperaMs;
    private readonly int _archivoLecturasEstables;

    public DocumentoProcesamientoService(
        BarcodeRegionService barcodeRegionService,
        ISoporteProcesamientoService soporteProcesamiento,
        IOptions<FileSettings> fileSettings,
        ILogger<DocumentoProcesamientoService> logger)
    {
        _barcodeRegionService = barcodeRegionService;
        _soporteProcesamiento = soporteProcesamiento;
        _logger = logger;
        _maxReintentos = fileSettings.Value.BarcodeMaxReintentos;
        _esperaMs = fileSettings.Value.BarcodeEsperaMs;
        _archivoEsperaIntentos = Math.Max(1, fileSettings.Value.ArchivoEsperaIntentos);
        _archivoEsperaMs = Math.Max(100, fileSettings.Value.ArchivoEsperaMs);
        _archivoLecturasEstables = Math.Max(1, fileSettings.Value.ArchivoLecturasEstables);
    }

    public async Task<DocumentoProcesamientoResult> ProcesarAsync(
        string rutaPdf,
        CancellationToken cancellationToken = default)
    {
        var nombreArchivo = Path.GetFileName(rutaPdf);

        try
        {
            await EsperarArchivoDisponible(rutaPdf, cancellationToken);

            if (!_barcodeRegionService.EsPdfLegible(rutaPdf))
            {
                _logger.LogWarning(
                    "PdfCorrupto | Archivo={Archivo} | Ruta={Ruta}",
                    nombreArchivo,
                    rutaPdf);

                return new DocumentoProcesamientoResult
                {
                    Estado = DocumentoProcesamientoEstado.PdfCorrupto,
                    Soporte = null,
                    IdPaciente = null
                };
            }

            var documento = await ProcesarConReintentos(rutaPdf, nombreArchivo, cancellationToken);

            if (documento == null)
            {
                return new DocumentoProcesamientoResult
                {
                    Estado = DocumentoProcesamientoEstado.FalloBarcode,
                    Soporte = null,
                    IdPaciente = null
                };
            }

            var soporte = $"{documento.Prefijo}{documento.Numero}";
            var resultado = await _soporteProcesamiento.ProcesarAsync(soporte, rutaPdf, cancellationToken);

            if (resultado.Estado == SoporteProcesamientoEstado.FalloApiDatos)
            {
                return new DocumentoProcesamientoResult
                {
                    Estado = DocumentoProcesamientoEstado.FalloApiDatos,
                    Documento = documento,
                    Soporte = soporte,
                    IdPaciente = null
                };
            }

            if (resultado.Estado == SoporteProcesamientoEstado.FalloApiFisico)
            {
                return new DocumentoProcesamientoResult
                {
                    Estado = DocumentoProcesamientoEstado.FalloApiFisico,
                    Documento = documento,
                    Soporte = soporte,
                    IdPaciente = resultado.Datos?.IdPaciente,
                    IdBodega = resultado.Datos?.IdBodega,
                    IdCartera = resultado.Datos?.IdCartera,
                    FechaFactura = resultado.Datos?.Fecha
                };
            }

            return new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.Exito,
                Documento = documento,
                Soporte = soporte,
                IdPaciente = resultado.Datos?.IdPaciente,
                IdBodega = resultado.Datos?.IdBodega,
                IdCartera = resultado.Datos?.IdCartera,
                FechaFactura = resultado.Datos?.Fecha
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorProcesandoDocumento | Archivo={Archivo} | Ruta={Ruta}",
                nombreArchivo,
                rutaPdf);

            return new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.ErrorInesperado,
                Soporte = null,
                IdPaciente = null
            };
        }
    }

    public async Task<DocumentoProcesamientoResult> ProcesarConCodigoConocidoAsync(
        string rutaPdf,
        DocumentoProcesadoDto documento,
        CancellationToken cancellationToken = default)
    {
        var soporte = $"{documento.Prefijo}{documento.Numero}";
        var resultado = await _soporteProcesamiento.ProcesarAsync(soporte, rutaPdf, cancellationToken);

        return resultado.Estado switch
        {
            SoporteProcesamientoEstado.FalloApiDatos => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.FalloApiDatos,
                Documento = documento,
                Soporte = soporte,
                IdPaciente = null
            },
            SoporteProcesamientoEstado.FalloApiFisico => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.FalloApiFisico,
                Documento = documento,
                Soporte = soporte,
                IdPaciente = resultado.Datos?.IdPaciente,
                IdBodega = resultado.Datos?.IdBodega,
                IdCartera = resultado.Datos?.IdCartera,
                FechaFactura = resultado.Datos?.Fecha
            },
            _ => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.Exito,
                Documento = documento,
                Soporte = soporte,
                IdPaciente = resultado.Datos?.IdPaciente,
                IdBodega = resultado.Datos?.IdBodega,
                IdCartera = resultado.Datos?.IdCartera,
                FechaFactura = resultado.Datos?.Fecha
            }
        };
    }

    public async Task<DocumentoProcesamientoResult> ProcesarConCodigoConocidoAsync(
        string rutaPdf,
        string soporte,
        CancellationToken cancellationToken = default)
    {
        var soporteConsulta = soporte.Trim();
        var resultado = await _soporteProcesamiento.ProcesarAsync(soporteConsulta, rutaPdf, cancellationToken);

        return resultado.Estado switch
        {
            SoporteProcesamientoEstado.FalloApiDatos => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.FalloApiDatos,
                Soporte = soporteConsulta,
                IdPaciente = null
            },
            SoporteProcesamientoEstado.FalloApiFisico => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.FalloApiFisico,
                Soporte = soporteConsulta,
                IdPaciente = resultado.Datos?.IdPaciente,
                IdBodega = resultado.Datos?.IdBodega,
                IdCartera = resultado.Datos?.IdCartera,
                FechaFactura = resultado.Datos?.Fecha
            },
            _ => new DocumentoProcesamientoResult
            {
                Estado = DocumentoProcesamientoEstado.Exito,
                Soporte = soporteConsulta,
                IdPaciente = resultado.Datos?.IdPaciente,
                IdBodega = resultado.Datos?.IdBodega,
                IdCartera = resultado.Datos?.IdCartera,
                FechaFactura = resultado.Datos?.Fecha
            }
        };
    }

    private async Task<DocumentoProcesadoDto?> ProcesarConReintentos(
        string ruta,
        string nombreArchivo,
        CancellationToken cancellationToken)
    {
        for (int intento = 1; intento <= _maxReintentos; intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var resultado = await Task.Run(
                    () => _barcodeRegionService.ProcesarPdf(ruta),
                    cancellationToken);

                if (resultado != null)
                {
                    if (intento > 1)
                    {
                        _logger.LogInformation(
                            "ReintentoExitoso | Archivo={Archivo} | Intento={Intento}",
                            nombreArchivo,
                            intento);
                    }

                    return resultado;
                }

                _logger.LogWarning(
                    "IntentoFallido | Archivo={Archivo} | Intento={Intento}",
                    nombreArchivo,
                    intento);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "ErrorEnIntento | Archivo={Archivo} | Intento={Intento}",
                    nombreArchivo,
                    intento);
            }

            await Task.Delay(_esperaMs, cancellationToken);
        }

        return null;
    }

    private async Task EsperarArchivoDisponible(string ruta, CancellationToken cancellationToken)
    {
        long ultimaLongitud = -1;
        int lecturasEstables = 0;
        Exception? ultimoError = null;

        for (int i = 0; i < _archivoEsperaIntentos; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    using var stream = AbrirArchivoParaLectura(ruta);
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
                        if (lecturasEstables >= _archivoLecturasEstables)
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

            await Task.Delay(_archivoEsperaMs, cancellationToken);
        }

        var detalle = ultimoError?.Message ?? "tiempo de espera agotado";
        throw new IOException(
            $"El archivo no está disponible tras {_archivoEsperaIntentos} intentos: {detalle}",
            ultimoError);
    }

    private static FileStream AbrirArchivoParaLectura(string ruta) =>
        new(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
