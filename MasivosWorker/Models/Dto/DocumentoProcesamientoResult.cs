namespace Models.Dto;

public class DocumentoProcesamientoResult
{
    public DocumentoProcesamientoEstado Estado { get; init; }

    public DocumentoProcesadoDto? Documento { get; init; }

    public string? Soporte { get; init; }

    public int? IdPaciente { get; init; }

    public string? IdBodega { get; init; }

    public string? IdCartera { get; init; }

    public DateTime? FechaFactura { get; init; }

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
