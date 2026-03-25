using IronBarCode;
using IronPdf;
using Microsoft.Extensions.Logging;
using Models.Dto;
using System.Drawing;
using System.Text.RegularExpressions;

namespace Services;

public class BarcodeRegionService
{
    private readonly ILogger<BarcodeRegionService> _logger;

    public BarcodeRegionService(ILogger<BarcodeRegionService> logger)
    {
        _logger = logger;
    }

    public DocumentoProcesadoDto ProcesarPdf(string rutaPdf)
    {
        try
        {
            if (!File.Exists(rutaPdf))
            {
                _logger.LogWarning($"Archivo no existe: {rutaPdf}");
                return null;
            }

            var codigo = LeerCodigoDesdePdf(rutaPdf);

            if (string.IsNullOrEmpty(codigo))
            {
                _logger.LogWarning("No se pudo leer código del PDF");
                return null;
            }

            // 🔥 Limpieza
            codigo = codigo.Replace(" ", "").Replace("-", "");

            // 🔥 Separar prefijo y número
            var match = Regex.Match(codigo, @"^([A-Z]+)(\d+)$");

            if (!match.Success)
            {
                _logger.LogWarning($"Código inválido: {codigo}");
                return null;
            }

            var prefijo = match.Groups[1].Value;
            var numero = match.Groups[2].Value;

            var archivoBytes = File.ReadAllBytes(rutaPdf);

            return new DocumentoProcesadoDto
            {
                Prefijo = prefijo,
                Numero = numero,
                NombreArchivo = $"{prefijo}{numero}.pdf",
                Archivo = archivoBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando PDF");
            return null;
        }
    }

    public string LeerCodigoDesdePdf(string rutaPdf)
    {
        try
        {
            using var pdf = PdfDocument.FromFile(rutaPdf);

            var imagenes = pdf.ToBitmap(600);

            var opciones = new BarcodeReaderOptions
            {
                Speed = ReadingSpeed.Detailed,
                ExpectMultipleBarcodes = true,
                Multithreaded = true,
                MaxParallelThreads = 4,
                AutoRotate = true,
                MinScanLines = 1,
                RemoveFalsePositive = false,
                ConfidenceThreshold = 0.5
            };

            foreach (var img in imagenes)
            {
                using var bitmap = (Bitmap)img;

                // 🔥 INTENTO 1
                var resultado = BarcodeReader.Read(bitmap, opciones);

                if (resultado != null && resultado.Count > 0)
                {
                    var codigo = resultado[0].Text;
                    _logger.LogInformation($"Barcode detectado: {codigo}");
                    return codigo;
                }

                // 🔥 INTENTO 2: BLOQUES
                int partes = 4;
                int ancho = bitmap.Width / partes;
                int alto = bitmap.Height / partes;

                for (int i = 0; i < partes; i++)
                {
                    for (int j = 0; j < partes; j++)
                    {
                        int x = i * ancho;
                        int y = j * alto;

                        int w = (i == partes - 1) ? bitmap.Width - x : ancho;
                        int h = (j == partes - 1) ? bitmap.Height - y : alto;

                        var rect = new Rectangle(x, y, w, h);

                        using var sub = bitmap.Clone(rect, bitmap.PixelFormat);

                        var res = BarcodeReader.Read(sub, opciones);

                        if (res != null && res.Count > 0)
                        {
                            var codigo = res[0].Text;
                            _logger.LogInformation($"Barcode detectado (bloque): {codigo}");
                            return codigo;
                        }
                    }
                }
            }

            _logger.LogWarning("No se detectó ningún código de barras");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo código de barras");
        }

        return null;
    }
}