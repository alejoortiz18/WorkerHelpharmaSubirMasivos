using IronBarCode;
using IronPdf;
using System.Drawing;
using System.Drawing.Imaging;

IronBarCode.License.LicenseKey =
    "IRONBARCODE.HELPHARMASAS.IRO260319.4409.24124-99E3C79258-DOFZGXMLX7KYCPU-P5V3Y6UY2Y7T-SGCNHU34HCQM-T6DNPK6J3LLN-TGNDYGPV6BU7-32YC72-L5327PU5ASWSEA-IRONBARCODE.DOTNET.LITE.PREMIUM.SUB-CRLKQ6.RENEW.SUPPORT.19.MAR.2027";

var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var docsDir = Path.Combine(baseDir, ".github", "DocumentosTest");

Console.WriteLine($"Directorio documentos: {docsDir}");

var archivos = new[]
{
    "scan1VariasH.pdf",
    "scan2-2H.pdf",
    "scan3-1H.pdf",
    "scan4-1H.pdf",
};

var opciones = new BarcodeReaderOptions
{
    Speed = ReadingSpeed.Balanced,
    AutoRotate = true,
    ExpectMultipleBarcodes = false,
    MinScanLines = 1,
    RemoveFalsePositive = false,
    ConfidenceThreshold = 0.5,
    Multithreaded = false,
    MaxParallelThreads = 1
};

foreach (var nombre in archivos)
{
    var ruta = Path.Combine(docsDir, nombre);
    var kb = new FileInfo(ruta).Length / 1024;
    Console.WriteLine($"\n{'=',0}{'=',0}{'=',0}{'=',0}{'=',0} {nombre} ({kb} KB) {'=',0}{'=',0}{'=',0}{'=',0}{'=',0}");

    try
    {
        using var pdf = PdfDocument.FromFile(ruta);
        var paginas = pdf.ToBitmap(400).ToList();
        Console.WriteLine($"  Paginas: {paginas.Count}");

        for (int i = 0; i < paginas.Count; i++)
        {
            using var bmp = (Bitmap)paginas[i];
            Console.WriteLine($"\n  -- Pagina {i + 1} ({bmp.Width} x {bmp.Height} px) --");

            string? resultado = null;

            // ESTRATEGIA 1: region superior derecha (55-100% ancho, 0-30% alto)
            var regionRect = new Rectangle(
                (int)(bmp.Width * 0.55), 0,
                (int)(bmp.Width * 0.45),
                (int)(bmp.Height * 0.30));
            using (var region = bmp.Clone(regionRect, bmp.PixelFormat))
            {
                var r1 = BarcodeReader.Read(region, opciones);
                if (r1?.Count > 0) { resultado = $"[E1-RegionNormal] {r1[0].Text}"; }

                if (resultado == null)
                {
                    using var mejorada = MejorarImagen(region);
                    var r2 = BarcodeReader.Read(mejorada, opciones);
                    if (r2?.Count > 0) resultado = $"[E2-RegionMejorada] {r2[0].Text}";
                }
            }

            // ESTRATEGIA 3: imagen completa
            if (resultado == null)
            {
                var r3 = BarcodeReader.Read(bmp, opciones);
                if (r3?.Count > 0) resultado = $"[E3-Completa] {r3[0].Text}";
            }

            // ESTRATEGIA 4: imagen completa mejorada
            if (resultado == null)
            {
                using var mejorada = MejorarImagen(bmp);
                var r4 = BarcodeReader.Read(mejorada, opciones);
                if (r4?.Count > 0) resultado = $"[E4-CompletaMejorada] {r4[0].Text}";
            }

            // ESTRATEGIA 5: bloques 2x2
            if (resultado == null)
            {
                int ancho = bmp.Width / 2, alto = bmp.Height / 2;
                for (int bi = 0; bi < 2 && resultado == null; bi++)
                    for (int bj = 0; bj < 2 && resultado == null; bj++)
                    {
                        using var sub = bmp.Clone(new Rectangle(bi * ancho, bj * alto, ancho, alto), bmp.PixelFormat);
                        var r5 = BarcodeReader.Read(sub, opciones);
                        if (r5?.Count > 0) resultado = $"[E5-Bloque({bi},{bj})] {r5[0].Text}";
                    }
            }

            if (resultado != null)
                Console.WriteLine($"    BARCODE => {Truncar(resultado, 300)}");
            else
                Console.WriteLine($"    >> Sin barcode detectado");
        }

        for (int i = 1; i < paginas.Count; i++) paginas[i]?.Dispose();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERROR: {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine("\nAnalisis finalizado.");

static Bitmap MejorarImagen(Bitmap original)
{
    var nueva = new Bitmap(original.Width, original.Height);
    using var g = Graphics.FromImage(nueva);
    var matrix = new ColorMatrix(new float[][]
    {
        new float[] {1.4f, 0, 0, 0, 0},
        new float[] {0, 1.4f, 0, 0, 0},
        new float[] {0, 0, 1.4f, 0, 0},
        new float[] {0, 0, 0, 1, 0},
        new float[] {-0.2f, -0.2f, -0.2f, 0, 1}
    });
    var attr = new ImageAttributes();
    attr.SetColorMatrix(matrix);
    g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attr);
    return nueva;
}

static string Truncar(string? valor, int max) =>
    valor is null ? "(null)" :
    valor.Length <= max ? valor : valor[..max] + "...";
