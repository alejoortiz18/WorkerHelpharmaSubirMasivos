namespace GestionArchivosEscaneados.Infrastructure.Barcode;

public enum OpenAiBarcodeResultKind
{
    CodigoEncontrado,
    NoBarcode,
    ErrorServicio
}

public sealed class OpenAiBarcodeResult
{
    public OpenAiBarcodeResultKind Tipo { get; init; }

    public string? Codigo { get; init; }

    public string? ErrorMensaje { get; init; }
}
