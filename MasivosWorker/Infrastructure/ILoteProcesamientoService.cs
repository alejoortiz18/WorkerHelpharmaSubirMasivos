namespace Infrastructure;

/// <summary>
/// Orquesta el ciclo completo de un lote (TXT) en UNC.
/// Abstracción para permitir sustituir el procesamiento en pruebas.
/// </summary>
public interface ILoteProcesamientoService
{
    Task<LoteProcesamientoOutcome> ProcesarLoteAsync(string rutaTxt, CancellationToken cancellationToken);
}
