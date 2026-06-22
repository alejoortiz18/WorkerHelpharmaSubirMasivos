namespace GestionArchivosEscaneados.Models.Settings;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o";

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxReintentos { get; set; } = 3;

    public string PromptResourcePath { get; set; } = "Prompts/barcode-openai.txt";
}
