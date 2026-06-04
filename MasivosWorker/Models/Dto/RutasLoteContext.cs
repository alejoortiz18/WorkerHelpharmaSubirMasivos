namespace Models.Dto;

/// <summary>
/// Rutas hermanas de un lote, derivadas desde la carpeta <c>procesar</c> leída del TXT.
/// </summary>
public class RutasLoteContext
{
    public required string Usuario { get; init; }

    public required string Fecha { get; init; }

    public required string Procesar { get; init; }

    public required string Procesando { get; init; }

    public required string Error { get; init; }

    public required string Procesaria { get; init; }

    public required string Noprocesados { get; init; }

    public required string Procesados { get; init; }

    public required string Log { get; init; }

    public string RutaLogDiario => Path.Combine(Log, $"{Fecha}.txt");

    public IEnumerable<string> CarpetasOperativas =>
    [
        Procesar,
        Procesando,
        Error,
        Procesaria,
        Noprocesados,
        Procesados,
        Log
    ];

    public IEnumerable<string> CarpetasLimpieza =>
    [
        Procesados,
        Procesando,
        Procesaria,
        Error
    ];
}
