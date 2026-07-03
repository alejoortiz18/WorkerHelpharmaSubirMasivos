using Models.Dto;

namespace Infrastructure;

public interface IRadicaWebIntegracionService
{
    Task ProcesarCombinacionesLoteAsync(
        RutasLoteContext contexto,
        IReadOnlyList<(DateOnly Fecha, string Bodega)> combinaciones,
        CancellationToken cancellationToken = default);
}
