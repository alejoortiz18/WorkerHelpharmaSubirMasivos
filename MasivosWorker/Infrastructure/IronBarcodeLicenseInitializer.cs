using IronBarCode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Infrastructure;

public class IronBarcodeLicenseInitializer
{
    public IronBarcodeLicenseInitializer(
        IOptions<IronBarcodeSettings> config,
        ILogger<IronBarcodeLicenseInitializer> logger)
    {
        var licenseKey = config.Value?.LicenseKey;

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            logger.LogCritical(
                "IronBarcode | LicenseKey no encontrada en la configuración. " +
                "Verifique que appsettings.json esté presente y contenga la sección 'IronBarcode'.");
            return;
        }

        License.LicenseKey = licenseKey;

        if (License.IsValidLicense(licenseKey))
        {
            logger.LogInformation(
                "IronBarcode | Licencia válida aplicada correctamente. Key=[{KeyPreview}...]",
                licenseKey[..Math.Min(30, licenseKey.Length)]);
        }
        else
        {
            logger.LogCritical(
                "IronBarcode | La licencia fue configurada pero NO es válida. " +
                "Verifique que la clave sea correcta y que el equipo tenga acceso a internet para validarla. " +
                "Key=[{KeyPreview}...]",
                licenseKey[..Math.Min(30, licenseKey.Length)]);
        }
    }
}