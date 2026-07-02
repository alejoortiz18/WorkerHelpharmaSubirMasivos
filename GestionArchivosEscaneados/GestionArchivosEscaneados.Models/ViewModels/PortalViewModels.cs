using GestionArchivosEscaneados.Models.Entities;
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

    public Dictionary<string, CalendarioDiaResumen> ResumenPorFecha { get; set; } =
        new(StringComparer.Ordinal);

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

    public int TotalConIntentoPrevio { get; set; }

    public int TotalPendientesReproceso { get; set; }

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

    public bool TieneIntentoPrevio { get; set; }

    public DateTime? FechaFactura { get; set; }
}

public class ProcesarLoteRequest
{
    public string Fecha { get; set; } = string.Empty;

    public string? ArchivoSeleccionado { get; set; }

    public List<ArchivoNoProcesadoItemViewModel> Archivos { get; set; } = [];
}

public class ReprocesarDocumentoRequest
{
    public string Fecha { get; set; } = string.Empty;

    public string NombreArchivo { get; set; } = string.Empty;
}

public class ProcesarDocumentoRequest
{
    public string Fecha { get; set; } = string.Empty;

    public string NombreArchivo { get; set; } = string.Empty;

    public string CodigoBarras { get; set; } = string.Empty;
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

public class TransaccionesUsuariosViewModel
{
    public IReadOnlyList<UsuarioEscaneoResumen> Usuarios { get; set; } = [];
}

public class TransaccionesFechasViewModel
{
    public string Usuario { get; set; } = string.Empty;

    public IReadOnlyList<FechaEscaneoResumen> Fechas { get; set; } = [];
}

public class TransaccionesDocumentosViewModel
{
    public string Usuario { get; set; } = string.Empty;

    public string Fecha { get; set; } = string.Empty;

    public IReadOnlyList<DocumentoProcesadoConsulta> Documentos { get; set; } = [];
}
