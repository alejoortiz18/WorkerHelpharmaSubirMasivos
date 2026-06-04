using GestionArchivosEscaneados.Models.Enums;

namespace GestionArchivosEscaneados.Models.Dto;

public class ReprocesoLoteItemResult
{
    public required string NombreArchivo { get; init; }

    public required string CodigoBarras { get; init; }

    public bool Exito { get; init; }

    public SoporteProcesamientoEstado Estado { get; init; }
}
