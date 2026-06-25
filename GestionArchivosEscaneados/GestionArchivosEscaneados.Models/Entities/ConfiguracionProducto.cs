namespace GestionArchivosEscaneados.Models.Entities;

public class ConfiguracionProducto
{
    public int ConfiguracionId { get; set; }

    public string Producto { get; set; } = string.Empty;

    public string? Endpoint { get; set; }

    public string? EndpointVerificacion { get; set; }

    public string? ClaveCredencial { get; set; }

    public string? ValorAdicional { get; set; }

    public string? Prompt { get; set; }

    public string? Descripcion { get; set; }

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaActualizacion { get; set; }
}
