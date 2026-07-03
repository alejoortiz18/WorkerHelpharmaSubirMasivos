using Infrastructure;
using Models.Dto;

namespace Tests.Infrastructure;

internal sealed class NoopRadicaWebIntegracionService : IRadicaWebIntegracionService
{
    public List<(RutasLoteContext Contexto, IReadOnlyList<(DateOnly Fecha, string Bodega)> Combinaciones)> Llamadas { get; } = [];

    public Task ProcesarCombinacionesLoteAsync(
        RutasLoteContext contexto,
        IReadOnlyList<(DateOnly Fecha, string Bodega)> combinaciones,
        CancellationToken cancellationToken = default)
    {
        Llamadas.Add((contexto, combinaciones));
        return Task.CompletedTask;
    }
}
