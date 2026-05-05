using IronBarCode;
using IronPdf;
using Microsoft.Extensions.Logging;
using Models.Dto;
using System.Drawing;
using System.Drawing.Imaging;
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

            codigo = codigo.Replace(" ", "").Replace("-", "");

            var match = Regex.Match(codigo, @"^([A-Z]+)(\d+)$");

            if (!match.Success)
            {
                _logger.LogWarning($"Código inválido: {codigo}");
                return null;
            }

            return new DocumentoProcesadoDto
            {
                Prefijo = match.Groups[1].Value,
                Numero = match.Groups[2].Value,
                NombreArchivo = $"{codigo}.pdf"
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

            _logger.LogInformation(
                "LeyendoPdf | Archivo={Archivo} | Paginas={Paginas}",
                Path.GetFileName(rutaPdf),
                pdf.PageCount
            );

            // Solo renderizar la primera página — el archivo PDF completo se conserva intacto
            using var primeraPagina = pdf.CopyPage(0);
            var imagenes = primeraPagina.ToBitmap(400).ToList();
            using var bitmap = (Bitmap)imagenes[0];

            var opciones = new BarcodeReaderOptions
            {
                Speed = ReadingSpeed.Balanced,
                AutoRotate = true,
                ExpectMultipleBarcodes = false,

                MinScanLines = 1,
                RemoveFalsePositive = false,
                ConfidenceThreshold = 0.5,

                Multithreaded = true,
                MaxParallelThreads = Environment.ProcessorCount
            };

            // 🔥 REGIÓN SUPERIOR DERECHA
            var regionRect = new Rectangle(
                (int)(bitmap.Width * 0.55),
                0,
                (int)(bitmap.Width * 0.45),
                (int)(bitmap.Height * 0.30) // 🔥 aumentamos altura
            );

            using (var region = bitmap.Clone(regionRect, bitmap.PixelFormat))
            {
                // 🔥 1. REGIÓN NORMAL
                var resultado = BarcodeReader.Read(region, opciones);

                if (resultado?.Count > 0)
                {
                    var codigo = resultado[0].Text;
                    _logger.LogInformation($"Barcode (región): {codigo}");
                    return codigo;
                }

                // 🔥 2. REGIÓN MEJORADA (CLAVE PARA ESTE PDF)
                using (var mejorada = MejorarImagen(region))
                {
                    var res2 = BarcodeReader.Read(mejorada, opciones);

                    if (res2?.Count > 0)
                    {
                        var codigo = res2[0].Text;
                        _logger.LogInformation($"Barcode (región mejorada): {codigo}");
                        return codigo;
                    }
                }
            }

            // 🔥 3. IMAGEN COMPLETA
            var resultadoCompleto = BarcodeReader.Read(bitmap, opciones);

            if (resultadoCompleto?.Count > 0)
            {
                var codigo = resultadoCompleto[0].Text;
                _logger.LogInformation($"Barcode completo: {codigo}");
                return codigo;
            }

            // 🔥 4. IMAGEN COMPLETA MEJORADA
            using (var mejorada = MejorarImagen(bitmap))
            {
                var res3 = BarcodeReader.Read(mejorada, opciones);

                if (res3?.Count > 0)
                {
                    var codigo = res3[0].Text;
                    _logger.LogInformation($"Barcode mejorado: {codigo}");
                    return codigo;
                }
            }

            // 🔥 5. BLOQUES (último recurso)
            int partes = 2;

            int ancho = bitmap.Width / partes;
            int alto = bitmap.Height / partes;

            for (int i = 0; i < partes; i++)
            {
                for (int j = 0; j < partes; j++)
                {
                    var rect = new Rectangle(i * ancho, j * alto, ancho, alto);

                    using var sub = bitmap.Clone(rect, bitmap.PixelFormat);

                    var res = BarcodeReader.Read(sub, opciones);

                    if (res?.Count > 0)
                    {
                        var codigo = res[0].Text;
                        _logger.LogInformation($"Barcode (bloque): {codigo}");
                        return codigo;
                    }
                }
            }

            _logger.LogWarning("No se detectó ningún código");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo código");
        }

        return null;
    }

    private Bitmap MejorarImagen(Bitmap original)
    {
        var nueva = new Bitmap(original.Width, original.Height);

        using (var g = Graphics.FromImage(nueva))
        {
            var matrix = new ColorMatrix(new float[][]
            {
                new float[] {1.4f, 0, 0, 0, 0},
                new float[] {0, 1.4f, 0, 0, 0},
                new float[] {0, 0, 1.4f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {-0.2f, -0.2f, -0.2f, 0, 1}
            });

            var atributos = new ImageAttributes();
            atributos.SetColorMatrix(matrix);

            g.DrawImage(
                original,
                new Rectangle(0, 0, original.Width, original.Height),
                0,
                0,
                original.Width,
                original.Height,
                GraphicsUnit.Pixel,
                atributos);
        }

        return nueva;
    }
}