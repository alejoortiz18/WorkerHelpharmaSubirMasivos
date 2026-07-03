namespace GestionArchivosEscaneados.Models.Entities;

public class RadicaWebNotificacionConsulta
{
    public long RadicaWebApiId { get; init; }

    public string NombreUsuario { get; init; } = string.Empty;

    public DateOnly FechaFactura { get; init; }

    public string Bodega { get; init; } = string.Empty;

    public bool? Success { get; init; }

    public string? Message { get; init; }

    public int? SolicitudId { get; init; }

    public int? RegistrosInsertados { get; init; }

    public int? TotalRegistros { get; init; }

    public string? JobId { get; init; }

    public int? StatusCode { get; init; }

    public string? Error { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string? Path { get; init; }

    public DateTime CreadoEn { get; init; }

    public bool PuedeRenotificar => Success == false;
}
