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
        string? idBodega,
        string? idCartera,
        DateTime? fechaFactura,
        bool procesado,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RegistrarRadicaWebAsync(
        RutasLoteContext contexto,
        DateOnly fechaFactura,
        string bodega,
        RadicaWebBusquedaResultado resultado,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<(DateOnly Fecha, string Bodega)>> ObtenerCombinacionesRadicaWebAsync(
        RutasLoteContext contexto,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<(DateOnly Fecha, string Bodega)>>([]);
}
