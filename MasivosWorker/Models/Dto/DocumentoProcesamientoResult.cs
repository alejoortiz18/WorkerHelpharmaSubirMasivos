namespace Models.Dto;

public class DocumentoProcesamientoResult
{
    public DocumentoProcesamientoEstado Estado { get; init; }

    public DocumentoProcesadoDto? Documento { get; init; }

    public bool EsExitoso => Estado == DocumentoProcesamientoEstado.Exito;
}

public enum DocumentoProcesamientoEstado
{
    Exito,
    FalloBarcode,
    FalloApiDatos,
    FalloApiFisico,
    PdfCorrupto,
    ErrorInesperado
}
