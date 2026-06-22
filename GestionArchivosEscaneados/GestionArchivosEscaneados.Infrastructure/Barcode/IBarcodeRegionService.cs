namespace GestionArchivosEscaneados.Infrastructure.Barcode;

public interface IBarcodeRegionService
{
    string? LeerCodigoDesdePdf(string rutaPdf);
}
