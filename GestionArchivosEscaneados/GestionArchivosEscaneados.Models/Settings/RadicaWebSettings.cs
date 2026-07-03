namespace GestionArchivosEscaneados.Models.Settings;

public class RadicaWebSettings
{
    public string ApiUrl { get; set; } =
        "https://api-radicacion.helpharma.com.co/api/physical-supports/integrations/busqueda";

    public string ApiClient { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(ApiClient) && !string.IsNullOrWhiteSpace(ApiSecret);
}
