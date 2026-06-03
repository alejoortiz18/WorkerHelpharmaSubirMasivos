namespace MoverDocumentos.Core.Configuration;

public class RutasSettings
{
    public string CarpetaLocal { get; set; } = @"C:\scaneo";
    public string RaizUnc { get; set; } = @"\\192.168.0.69\ArchivosScaneados";
    public string CarpetaArchivosNuevos { get; set; } = "ArchivosNuevos";
    public string CarpetaUsuarios { get; set; } = "Usuarios";
    public string ArchivoUsuarios { get; set; } = "usuarios.txt";
    public string[] SubcarpetasDia { get; set; } =
    [
        "procesar",
        "procesando",
        "procesaria",
        "noprocesados",
        "procesados",
        "error",
        "log"
    ];

    public string RutaArchivosNuevos =>
        Path.Combine(RaizUnc, CarpetaArchivosNuevos);

    public string RutaUsuarios =>
        Path.Combine(RaizUnc, CarpetaUsuarios);

    public string RutaArchivoUsuarios =>
        Path.Combine(RutaUsuarios, ArchivoUsuarios);

    /// <summary>Ruta UNC normalizada para escribir en TXT de lote (Worker 2).</summary>
    public string ObtenerRutaCarpetaProcesar(string usuario, DateOnly fecha) =>
        NormalizarRutaRed(Path.Combine(
            RaizUnc,
            usuario.ToLowerInvariant(),
            fecha.ToString("yyyy-MM-dd"),
            "procesar"));

    public static string NormalizarRutaRed(string ruta) =>
        ruta.Replace('/', '\\').TrimEnd('\\');
}
