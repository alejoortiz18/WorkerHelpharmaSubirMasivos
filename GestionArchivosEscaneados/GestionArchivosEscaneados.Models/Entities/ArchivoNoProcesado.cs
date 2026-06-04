namespace GestionArchivosEscaneados.Models.Entities;

public class ArchivoNoProcesado
{
    public required string NombreArchivo { get; init; }

    public required string Fecha { get; init; }

    public required string RutaCompleta { get; init; }
}
