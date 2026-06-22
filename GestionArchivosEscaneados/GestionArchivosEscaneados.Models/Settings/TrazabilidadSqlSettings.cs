namespace GestionArchivosEscaneados.Models.Settings;

public class TrazabilidadSqlSettings
{
    public string ConnectionString { get; set; } =
        @"Server=ServiciosReleas\SQLEXPRESS;Database=Scaneados;Trusted_Connection=True;TrustServerCertificate=True;";
}
