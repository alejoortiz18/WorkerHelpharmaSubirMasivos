namespace Models.Dto;

public class OpenAiBarcodeResult
{
    public OpenAiBarcodeResultKind Tipo { get; init; }

    public string? Codigo { get; init; }

    public string? ErrorMensaje { get; init; }

    public DocumentoProcesadoDto? Documento { get; init; }
}

public enum OpenAiBarcodeResultKind
{
    CodigoEncontrado,
    NoBarcode,
    ErrorServicio
}
