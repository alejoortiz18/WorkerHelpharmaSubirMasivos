namespace GestionArchivosEscaneados.Models.Entities;

public class RadicaWebKpiResumen
{
    public int Total { get; init; }

    public int Exitosas { get; init; }

    public int Fallidas { get; init; }

    public int SinResultado { get; init; }

    public int UsuariosDistintos { get; init; }

    public int BodegasDistintas { get; init; }
}

public class RadicaWebUsuarioKpi
{
    public string NombreUsuario { get; init; } = string.Empty;

    public int Total { get; init; }

    public int Exitosas { get; init; }

    public int Fallidas { get; init; }

    public int SinNotificar { get; init; }
}

public class RadicaWebBodegaKpi
{
    public string Bodega { get; init; } = string.Empty;

    public int Total { get; init; }

    public int Exitosas { get; init; }

    public int Fallidas { get; init; }
}

public class RadicaWebFechaFacturaKpi
{
    public DateOnly FechaFactura { get; init; }

    public int Total { get; init; }

    public int Exitosas { get; init; }

    public int Fallidas { get; init; }
}
