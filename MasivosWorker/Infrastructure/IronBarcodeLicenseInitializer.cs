using IronBarCode;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Infrastructure;

public class IronBarcodeLicenseInitializer
{
    public IronBarcodeLicenseInitializer(IOptions<IronBarcodeSettings> config)
    {
        License.LicenseKey = config.Value.LicenseKey;
    }
}