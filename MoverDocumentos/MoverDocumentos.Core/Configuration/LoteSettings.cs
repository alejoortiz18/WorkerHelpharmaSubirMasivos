namespace MoverDocumentos.Core.Configuration;

public class LoteSettings
{
    public int SegundosInactividadParaCerrarLote { get; set; } = 60;
    public string FormatoHoraEnNombreTxt { get; set; } = "hh-mm-sstt";
}
