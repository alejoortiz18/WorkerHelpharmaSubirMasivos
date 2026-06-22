namespace GestionArchivosEscaneados.Models.Settings;

public class RutasSettings
{
    public string RaizUnc { get; set; } = string.Empty;

    public string CarpetaUsuario(string usuario) =>
        Path.Combine(RaizUnc.TrimEnd('\\'), usuario);
}
