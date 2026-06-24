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

    /// <summary>Texto crudo devuelto por OpenAI antes de normalizar.</summary>
    public string? RespuestaCruda { get; set; }

    public string? ErrorMensaje { get; init; }
}
