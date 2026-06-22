namespace GestionArchivosEscaneados.Models.Enums;

public enum SoporteProcesamientoEstado
{
    Exito,
    FalloApiDatos,
    FalloApiFisico,
    FalloBarcode,
    PdfCorrupto,
    FalloOpenAi,
    ErrorInesperado
}
