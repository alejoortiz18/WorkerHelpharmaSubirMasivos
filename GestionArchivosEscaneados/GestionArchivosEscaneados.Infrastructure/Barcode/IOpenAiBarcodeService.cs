namespace GestionArchivosEscaneados.Infrastructure.Barcode;

public interface IOpenAiBarcodeService
{
    Task<OpenAiBarcodeResult> LeerCodigoAsync(string rutaPdf, CancellationToken cancellationToken = default);
}
