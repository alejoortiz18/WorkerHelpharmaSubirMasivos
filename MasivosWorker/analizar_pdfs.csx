using IronBarCode;
using IronPdf;
using System;
using System.IO;
using System.Drawing;

// Clave de licencia (leer desde appsettings.json)
IronBarCode.License.LicenseKey = "IRONBARCODE.HELPHARMASAS.IRN230421.4673.51841.8741985-K3JZLQ5BTR-R6HTFEMXASP5-3HMMB6TU2HBU-FXTKBKGPAQWY-JYTYXDW234YN-LQ55VH5RPFQO-5EFZHJ-JCQBTIEAB5OMIA-DEPLOYMENT.TRIAL.EXPIRES.23.APR.2026";

var docs = new[] {
    ".github\\DocumentosTest\\scan1VariasH.pdf",
    ".github\\DocumentosTest\\scan2-2H.pdf",
    ".github\\DocumentosTest\\scan3-1H.pdf",
    ".github\\DocumentosTest\\scan4-1H.pdf",
};

foreach (var doc in docs)
{
    Console.WriteLine($"\n=== {Path.GetFileName(doc)} ({new FileInfo(doc).Length / 1024} KB) ===");
    try
    {
        using var pdf = new PdfDocument(doc);
        var bitmaps = pdf.ToBitmap(400).ToList();
        Console.WriteLine($"  Páginas: {bitmaps.Count}");
        for (int i = 0; i < bitmaps.Count; i++)
        {
            using var bmp = (Bitmap)bitmaps[i];
            Console.WriteLine($"  Página {i+1}: {bmp.Width}x{bmp.Height} px");
            var result = BarcodeReader.Read(bmp, new BarcodeReaderOptions { ExpectBarcodeTypes = BarcodeEncoding.PDF417 | BarcodeEncoding.QRCode | BarcodeEncoding.Code128 | BarcodeEncoding.DataMatrix, Multithreaded = false, MaxParallelThreads = 1 });
            if (result.Count > 0)
                foreach (var b in result) Console.WriteLine($"    BARCODE [{b.BarcodeType}]: {b.Value?.Substring(0, Math.Min(b.Value.Length, 120))}");
            else
                Console.WriteLine($"    (sin barcode en lectura directa)");
        }
        for (int i = 1; i < bitmaps.Count; i++) bitmaps[i].Dispose();
    }
    catch (Exception ex) { Console.WriteLine($"  ERROR: {ex.Message}"); }
}
