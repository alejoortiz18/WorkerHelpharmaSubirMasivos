namespace GestionArchivosEscaneados.Models.Entities;

public class UsuarioEscaneoResumen
{
    public string NombreUsuario { get; init; } = string.Empty;

    public int CantidadDiasEscaneados { get; init; }

    public string UltimoDiaEscaneado { get; init; } = string.Empty;
}
