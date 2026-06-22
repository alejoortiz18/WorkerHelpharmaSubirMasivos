namespace Models.Dto;

public class EmailSettings
{
    public string Remitente { get; set; } = string.Empty;

    public List<string> Destinatarios { get; set; } = [];

    public List<string> DestinatariosPendientes { get; set; } = [];

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string Usuario { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public bool Habilitado =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        Destinatarios.Count > 0;
}
