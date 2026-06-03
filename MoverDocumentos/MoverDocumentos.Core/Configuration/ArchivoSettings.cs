namespace MoverDocumentos.Core.Configuration;

public class ArchivoSettings
{
    public int EsperaIntentos { get; set; } = 120;
    public int EsperaMs { get; set; } = 500;
    public int LecturasEstables { get; set; } = 2;
    public int EscaneoRespaldoSegundos { get; set; } = 30;
}
