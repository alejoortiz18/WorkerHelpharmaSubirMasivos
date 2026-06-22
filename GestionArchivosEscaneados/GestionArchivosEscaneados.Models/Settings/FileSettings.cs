namespace GestionArchivosEscaneados.Models.Settings;

public class FileSettings
{
    public int BarcodeMaxReintentos { get; set; } = 3;

    public int BarcodeEsperaMs { get; set; } = 500;

    public int ArchivoEsperaIntentos { get; set; } = 6;

    public int ArchivoEsperaMs { get; set; } = 500;

    public int ArchivoLecturasEstables { get; set; } = 2;
}
