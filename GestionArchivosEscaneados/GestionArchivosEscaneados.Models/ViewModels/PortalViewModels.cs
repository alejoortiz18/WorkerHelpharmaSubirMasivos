using GestionArchivosEscaneados.Models.Enums;

namespace GestionArchivosEscaneados.Models.ViewModels;

public class LoginViewModel
{
    public string Usuario { get; set; } = string.Empty;

    public string? Error { get; set; }
}

public class CalendarioViewModel
{
    public int Anio { get; set; }

    public int Mes { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public HashSet<string> FechasDisponibles { get; set; } = [];

    public string? Error { get; set; }
}

public class DashboardViewModel
{
    public string Fecha { get; set; } = string.Empty;

    public int CantidadProcesados { get; set; }

    public int NoProcesados { get; set; }
}

public class NoProcesadosViewModel
{
    public string Fecha { get; set; } = string.Empty;

    public List<ArchivoNoProcesadoItemViewModel> Archivos { get; set; } = [];

    public string? ArchivoSeleccionado { get; set; }

    public string? MensajeExito { get; set; }

    public string? MensajeError { get; set; }

    public string? MensajeAdvertencia { get; set; }
}

public class ArchivoNoProcesadoItemViewModel
{
    public string NombreArchivo { get; set; } = string.Empty;

    public string Fecha { get; set; } = string.Empty;

    public string CodigoBarras { get; set; } = string.Empty;

    public bool ErrorProceso { get; set; }
}

public class ProcesarLoteRequest
{
    public string Fecha { get; set; } = string.Empty;

    public string? ArchivoSeleccionado { get; set; }

    public List<ArchivoNoProcesadoItemViewModel> Archivos { get; set; } = [];
}

public class EliminarDocumentoRequest
{
    public string Fecha { get; set; } = string.Empty;

    public string NombreArchivo { get; set; } = string.Empty;
}

public class ReprocesoResultViewModel
{
    public bool Exito { get; set; }

    public SoporteProcesamientoEstado Estado { get; set; }
}
