using Models.Dto;

namespace Infrastructure;

public interface IDocumentoProcesamientoService
{
    Task<DocumentoProcesamientoResult> ProcesarAsync(
        string rutaPdf,
        CancellationToken cancellationToken = default);

    Task<DocumentoProcesamientoResult> ProcesarConCodigoConocidoAsync(
        string rutaPdf,
        DocumentoProcesadoDto documento,
        CancellationToken cancellationToken = default);
}
