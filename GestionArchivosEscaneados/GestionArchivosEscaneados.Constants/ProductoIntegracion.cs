namespace GestionArchivosEscaneados.Constants;

public static class ProductoIntegracion
{
    public const string Unc = "Carpeta UNC / NAS";
    public const string SoporteApi = "API DatosSoportes";
    public const string SoporteFisico = "API Soporte físico";
    public const string OpenAi = "OpenAI";
    public const string RadicaWeb = "API RadicaWeb";

    public static readonly IReadOnlyList<string> Todos =
    [
        Unc,
        SoporteApi,
        SoporteFisico,
        OpenAi,
        RadicaWeb
    ];

    public static string IdDesdeProducto(string producto) =>
        producto switch
        {
            Unc => "unc",
            SoporteApi => "soporte-api",
            SoporteFisico => "soporte-fisico",
            OpenAi => "openai",
            RadicaWeb => "radicaweb",
            _ => producto.ToLowerInvariant().Replace(' ', '-')
        };

    public static string? ProductoDesdeId(string id) =>
        id switch
        {
            "unc" => Unc,
            "soporte-api" => SoporteApi,
            "soporte-fisico" => SoporteFisico,
            "openai" => OpenAi,
            "radicaweb" => RadicaWeb,
            _ => null
        };
}

public static class IntegracionDefaults
{
    public const string SoporteApiUrl =
        "https://api-soportes.helpharma.com.co/api/DocSoporte/soportes/DatosSoportes";

    public const string SoporteFisicoApiUrl =
        "https://intranet.helpharma.com/api/v1/soporte/fisico";

    public const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";

    public const string OpenAiModelsUrl = "https://api.openai.com/v1/models";

    public const string RadicaWebApiUrl =
        "https://api-radicacion.helpharma.com.co/api/physical-supports/integrations/busqueda";
}
