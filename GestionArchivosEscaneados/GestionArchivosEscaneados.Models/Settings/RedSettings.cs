namespace GestionArchivosEscaneados.Models.Settings;

public class RedSettings
{
    public string Usuario { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public bool UsarCredencialesConfiguradas { get; set; }
}
