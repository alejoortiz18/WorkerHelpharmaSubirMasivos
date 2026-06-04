namespace GestionArchivosEscaneados.Models.Settings;

public class RutasSettings
{
    public string RaizUnc { get; set; } = string.Empty;

    public string CarpetaUsuarios { get; set; } = "Usuarios";

    public string ArchivoUsuarios { get; set; } = "usuarios.txt";

    public string RutaArchivoUsuarios =>
        string.IsNullOrWhiteSpace(RaizUnc)
            ? string.Empty
            : Path.Combine(RaizUnc.TrimEnd('\\'), CarpetaUsuarios, ArchivoUsuarios);

    public string CarpetaUsuario(string usuario) =>
        Path.Combine(RaizUnc.TrimEnd('\\'), usuario);
}
