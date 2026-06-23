namespace GestionArchivosEscaneados.Models.Entities;

public class DocumentoPendiente
{
    public required string NombreArchivo { get; init; }

    public bool TieneIntentoPrevio { get; init; }

    public DateTime? FechaFactura { get; init; }
}
