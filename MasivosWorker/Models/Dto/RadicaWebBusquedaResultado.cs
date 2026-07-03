namespace Models.Dto;

/// <summary>
/// Resultado unificado de una llamada al API RadicaWeb (éxito, error HTTP o excepción).
/// </summary>
public sealed class RadicaWebBusquedaResultado
{
    public int? HttpStatusCode { get; init; }

    public bool? Success { get; init; }

    public string? Message { get; init; }

    public int? SolicitudId { get; init; }

    public int? RegistrosInsertados { get; init; }

    public int? TotalRegistros { get; init; }

    public string? JobId { get; init; }

    public string? Error { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string? Path { get; init; }
}
