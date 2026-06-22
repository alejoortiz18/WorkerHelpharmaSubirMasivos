using Infrastructure;
using Models.Dto;

namespace Tests.Infrastructure;

internal sealed class NoopTrazabilidadSqlService : ITrazabilidadSqlService
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RegistrarDocumentoAsync(
        RutasLoteContext contexto,
        string nombreArchivo,
        string? soporte,
        int? idPaciente,
        bool procesado,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
