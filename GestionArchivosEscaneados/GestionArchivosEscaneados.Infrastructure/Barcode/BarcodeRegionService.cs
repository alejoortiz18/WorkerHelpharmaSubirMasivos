using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using IronBarCode;
using IronPdf;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Barcode;

public class BarcodeRegionService : IBarcodeRegionService
{
    private readonly ILogger<BarcodeRegionService> _logger;

    public BarcodeRegionService(
        IOptions<IronBarcodeSettings> settings,
        ILogger<BarcodeRegionService> logger)
    {
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(settings.Value.LicenseKey))
        {
            IronBarCode.License.LicenseKey = settings.Value.LicenseKey;
        }
    }

    public string? LeerCodigoDesdePdf(string rutaPdf)
    {
        try
        {
            using var pdf = PdfDocument.FromFile(rutaPdf);

            _logger.LogInformation(
                "LeyendoPdf | Archivo={Archivo} | Paginas={Paginas}",
                Path.GetFileName(rutaPdf),
                pdf.PageCount);

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

            var regionRect = new Rectangle(
                (int)(bitmap.Width * 0.55),
                0,
                (int)(bitmap.Width * 0.45),
                (int)(bitmap.Height * 0.30));

            using (var region = bitmap.Clone(regionRect, bitmap.PixelFormat))
            {
                var resultado = BarcodeReader.Read(region, opciones);
                if (resultado?.Count > 0)
                    return NormalizarCodigo(resultado[0].Text);

                using var mejorada = MejorarImagen(region);
                var res2 = BarcodeReader.Read(mejorada, opciones);
                if (res2?.Count > 0)
                    return NormalizarCodigo(res2[0].Text);
            }

            var resultadoCompleto = BarcodeReader.Read(bitmap, opciones);
            if (resultadoCompleto?.Count > 0)
                return NormalizarCodigo(resultadoCompleto[0].Text);

            using (var mejorada = MejorarImagen(bitmap))
            {
                var res3 = BarcodeReader.Read(mejorada, opciones);
                if (res3?.Count > 0)
                    return NormalizarCodigo(res3[0].Text);
            }

            var partes = 2;
            var ancho = bitmap.Width / partes;
            var alto = bitmap.Height / partes;

            for (var i = 0; i < partes; i++)
            {
                for (var j = 0; j < partes; j++)
                {
                    var rect = new Rectangle(i * ancho, j * alto, ancho, alto);
                    using var sub = bitmap.Clone(rect, bitmap.PixelFormat);
                    var res = BarcodeReader.Read(sub, opciones);
                    if (res?.Count > 0)
                        return NormalizarCodigo(res[0].Text);
                }
            }

            _logger.LogWarning("No se detectó ningún código");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo código");
            return null;
        }
    }

    private static string? NormalizarCodigo(string? codigo)
    {
        var limpio = (codigo ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(limpio)
            ? null
            : Regex.IsMatch(limpio, @"^([A-Z]+)(\d+)$")
                ? limpio
                : null;
    }

    private static Bitmap MejorarImagen(Bitmap original)
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
