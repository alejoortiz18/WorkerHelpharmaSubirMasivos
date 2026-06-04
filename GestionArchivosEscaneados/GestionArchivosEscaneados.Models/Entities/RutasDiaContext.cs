namespace GestionArchivosEscaneados.Models.Entities;

public class RutasDiaContext
{
    public required string Usuario { get; init; }

    public required string Fecha { get; init; }

    public required string Procesar { get; init; }

    public required string Noprocesados { get; init; }

    public required string Procesados { get; init; }

    public required string Log { get; init; }

    public string RutaLogDiario => Path.Combine(Log, $"{Fecha}.txt");
}
