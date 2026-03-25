using IronBarCode;
using IronPdf;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Services;

public class BarcodeService
{
    private readonly ILogger<BarcodeService> _logger;

    public BarcodeService(ILogger<BarcodeService> logger)
    {
        _logger = logger;
    }

    public string ObtenerCodigo(string rutaPdf)
    {
        try
        {
            // 1. Intentar leer código de barras
            var codigo = LeerDesdeBarcode(rutaPdf);

            if (!string.IsNullOrEmpty(codigo))
                return codigo;

            // 2. Fallback → leer texto
            _logger.LogWarning("No se detectó barcode, usando OCR/texto");

            return LeerDesdeTexto(rutaPdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo código");
            return null;
        }
    }

    private string LeerDesdeBarcode(string rutaPdf)
    {
        try
        {
            var pdf = PdfDocument.FromFile(rutaPdf);
            var imagenes = pdf.ToBitmap(600);

            foreach (var img in imagenes)
            {
                var resultados = BarcodeReader.Read(img);

                if (resultados != null && resultados.Count > 0)
                {
                    var codigo = resultados[0].Text;

                    _logger.LogInformation($"Barcode detectado: {codigo}");
                    return codigo;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo barcode");
        }

        return null;
    }

    private string LeerDesdeTexto(string rutaPdf)
    {
        try
        {
            var pdf = PdfDocument.FromFile(rutaPdf);
            var texto = pdf.ExtractAllText();

            _logger.LogInformation("Texto extraído del PDF");

            // 🔥 PATRÓN CLAVE (ajustado a tu documento)
            var match = Regex.Match(texto, @"KE\d{6}");

            if (match.Success)
            {
                var codigo = match.Value;
                _logger.LogInformation($"Código encontrado por texto: {codigo}");
                return codigo;
            }

            _logger.LogWarning("No se encontró código en texto");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo texto del PDF");
        }

        return null;
    }
}