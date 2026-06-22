using Models.Dto;

namespace Services;

public interface ISoporteProcesamientoService
{
    Task<SoporteProcesamientoResult> ProcesarAsync(
        string soporte,
        string rutaArchivoPdf,
        CancellationToken cancellationToken = default);
}
