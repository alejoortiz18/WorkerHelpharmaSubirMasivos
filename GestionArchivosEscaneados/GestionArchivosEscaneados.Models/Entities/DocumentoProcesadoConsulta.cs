namespace GestionArchivosEscaneados.Models.Entities;

public class DocumentoProcesadoConsulta
{
    public long DocumentoProcesadoId { get; init; }

    public string NombreArchivo { get; init; } = string.Empty;

    public string? Soporte { get; init; }

    public int? IdPaciente { get; init; }

    public string? IdBodega { get; init; }

    public string? IdCartera { get; init; }

    public DateTime? FechaFactura { get; init; }

    public bool Procesado { get; init; }

    public DateTime FechaCreacion { get; init; }
}
