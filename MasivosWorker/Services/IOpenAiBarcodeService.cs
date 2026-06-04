using Models.Dto;

namespace Services;

public interface IOpenAiBarcodeService
{
    Task<OpenAiBarcodeResult> LeerCodigoAsync(
        string rutaPdf,
        CancellationToken cancellationToken = default);
}
