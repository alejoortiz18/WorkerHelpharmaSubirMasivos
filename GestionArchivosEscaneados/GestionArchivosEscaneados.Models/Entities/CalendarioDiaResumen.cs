namespace GestionArchivosEscaneados.Models.Entities;

public class CalendarioDiaResumen
{
    public string Fecha { get; init; } = string.Empty;

    public int TotalEscaneados { get; init; }

    public int NoProcesados { get; init; }
}
