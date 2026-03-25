using IronBarCode;
using IronPdf;
using Microsoft.Extensions.Logging;
using System.Drawing;

namespace Services;

public class BarcodeRegionService
{
    private readonly ILogger<BarcodeRegionService> _logger;

    public BarcodeRegionService(ILogger<BarcodeRegionService> logger)
    {
        _logger = logger;
    }


    public string LeerCodigoDesdePdf(string rutaPdf)
    {
        try
        {
            var pdf = PdfDocument.FromFile(rutaPdf);

            var imagenes = pdf.ToBitmap(600); // 🔥 CLAVE

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

                // INTENTO 1
                var resultado = BarcodeReader.Read(bitmap, opciones);

                if (resultado != null && resultado.Count > 0)
                    return resultado[0].Text;

                // INTENTO 2: BLOQUES
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
                            return res[0].Text;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return null;
    }



    private Bitmap Recortar(Bitmap original, int ancho, int alto, int margen)
    {
        try
        {
            int x = original.Width - ancho - margen;
            int y = margen;

            if (x < 0 || y < 0 || x + ancho > original.Width || y + alto > original.Height)
                return null;

            var rect = new Rectangle(x, y, ancho, alto);
            return original.Clone(rect, original.PixelFormat);
        }
        catch
        {
            return null;
        }
    }
}